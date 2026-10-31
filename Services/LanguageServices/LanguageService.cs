using CodingAgent.Interfaces;
using CodingAgent.Services.DevEnvironment;
using System.ComponentModel;

namespace CodingAgent.Services.LanguageServices;

public class LanguageService(string root, IProcessRunner? processRunner = null)
{
    public string Root { get; } = root;

    protected IProcessRunner ProcessRunner { get; } = processRunner ?? LanguageServices.ProcessRunner.Default;

    protected static bool IsWindows => OperatingSystem.IsWindows();

    protected static string NullDevice => IsWindows ? "NUL" : "/dev/null";

    protected async Task<string> RunProcessAsync(string executable, IReadOnlyList<string> arguments)
    {
        try
        {
            return await ProcessRunner.RunAsync(executable, Root, arguments, FormatCommand(executable, arguments));
        }
        catch (Win32Exception)
        {
            return ToolchainCatalog.MissingToolMessage(executable);
        }
    }

    protected static bool Succeeded(string result) => result.Contains("\nExit code: 0\n", StringComparison.Ordinal);

    protected bool HasFile(params string[] names) => names.Any(name => File.Exists(Path.Combine(Root, name)));

    // Processes run without a shell, so wildcards such as *.c are expanded here.
    protected List<string> FindSourceFiles(params string[] extensions)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(Root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                files.AddRange(Directory.EnumerateFiles(current)
                    .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/')));

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    if (!Constants.ExcludedDirectories.Contains(Path.GetFileName(directory)))
                        pending.Push(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    // Native executables get .exe on Windows; paths are relative to Root, which is the working directory.
    protected static string ExecutableName(string baseName) => IsWindows ? baseName + ".exe" : baseName;

    protected Task<string> RunExecutableAsync(string relativePath, string missingMessage) =>
        File.Exists(Path.Combine(Root, relativePath))
            ? RunProcessAsync(Path.Combine(Root, relativePath), [])
            : Task.FromResult(missingMessage);

    protected static bool IsTestFile(string relativePath)
    {
        var parts = relativePath.Split('/');
        var name = Path.GetFileNameWithoutExtension(relativePath);
        return parts.Take(parts.Length - 1).Any(part => part.Equals("test", StringComparison.OrdinalIgnoreCase) || part.Equals("tests", StringComparison.OrdinalIgnoreCase)) ||
               name.StartsWith("test_", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("test", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("tests", StringComparison.OrdinalIgnoreCase);
    }

    protected static string NoSourcesMessage(string kind) => $"ERROR: No {kind} source files were found in the project root.";

    private static string FormatCommand(string executable, IEnumerable<string> arguments) =>
        String.Join(" ", new[] { executable }.Concat(arguments).Select(part => part.Contains(' ') ? $"\"{part}\"" : part));
}
