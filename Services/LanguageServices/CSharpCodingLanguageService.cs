using CodingAgent.Interfaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace CodingAgent.Services.LanguageServices;

public class CSharpCodingLanguageService(string root, string projectFile, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private readonly string projectFile = projectFile;

    public Task<string> BuildAsync() => RunDotnetAsync("build", "--configuration", "Debug", "--no-incremental", "-warnaserror");

    public async Task<string> RunTestsAsync()
    {
        var resultsDirectory = Path.Combine(Path.GetTempPath(), "codingagent-coverage", RootHash, Path.GetRandomFileName());

        var result = await RunDotnetAsync("test", "--collect:XPlat Code Coverage", "--results-directory", resultsDirectory);

        return result + GetCoverageSummary(resultsDirectory);
    }

    // The compiler is the syntax checker for C#.
    public Task<string> ValidateSyntaxAsync() => BuildAsync();

    // When the agent works on its own source tree, a normal build would write into the running
    // agent's bin folder: the locked exe fails the copy and the clean step deletes its runtime files.
    private bool IsOwnSourceTree
    {
        get
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(Root), AppContext.BaseDirectory);
            return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }
    }

    private string RootHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(Root).ToLowerInvariant())))[..12];

    private Task<string> RunDotnetAsync(string command, params string[] options)
    {
        var args = new List<string> { command };
        args.AddRange(options);

        if (IsOwnSourceTree)
            args.AddRange(["--artifacts-path", Path.Combine(Path.GetTempPath(), "codingagent-build", RootHash)]);

        if (!String.IsNullOrWhiteSpace(projectFile))
            args.Add(projectFile);

        return RunProcessAsync("dotnet", args);
    }

    private static string GetCoverageSummary(string resultsDirectory)
    {
        try
        {
            var reports = Directory.Exists(resultsDirectory)
                ? Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
                : [];

            if (reports.Length == 0)
                return "\nCoverage: not collected (the test project needs the coverlet.collector package).";

            var lines = new List<string>();
            foreach (var report in reports)
            {
                var coverage = XDocument.Load(report).Root;
                var lineRate = Double.Parse(coverage?.Attribute("line-rate")?.Value ?? "0", CultureInfo.InvariantCulture);
                var branchRate = Double.Parse(coverage?.Attribute("branch-rate")?.Value ?? "0", CultureInfo.InvariantCulture);
                lines.Add($"Coverage: {lineRate:P1} lines, {branchRate:P1} branches");
            }

            return "\n" + String.Join("\n", lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or FormatException)
        {
            return $"\nCoverage: could not read the report ({ex.Message}).";
        }
        finally
        {
            try { Directory.Delete(resultsDirectory, true); } catch { }
        }
    }
}
