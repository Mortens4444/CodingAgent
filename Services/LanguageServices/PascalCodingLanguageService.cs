using CodingAgent.Interfaces;
using System.Text.RegularExpressions;

namespace CodingAgent.Services.LanguageServices;

// The main program is the .lpr file, main.pas, or the first .pas file that starts with "program".
// A test program (test.pas, tests.pas, or a program under test/ or tests/) is compiled and run by run_tests.
public partial class PascalCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    public Task<string> BuildAsync()
    {
        var main = FindProgram(test: false);
        return main is null
            ? Task.FromResult(NoSourcesMessage("Pascal program"))
            : RunProcessAsync("fpc", ["-vewn", main]);
    }

    public async Task<string> RunTestsAsync()
    {
        var test = FindProgram(test: true);
        if (test is null)
        {
            var main = FindProgram(test: false);
            return main is null
                ? NoSourcesMessage("Pascal program")
                : await RunExecutableAsync(ExecutableFor(main), $"No test program found and {ExecutableFor(main)} is not built. Run build_project first or add tests/test.pas.");
        }

        var compile = await RunProcessAsync("fpc", ["-vewn", test]);
        return Succeeded(compile)
            ? await RunExecutableAsync(ExecutableFor(test), $"ERROR: {ExecutableFor(test)} was not produced.")
            : compile;
    }

    public async Task<string> ValidateSyntaxAsync()
    {
        var main = FindProgram(test: false);
        return main is null
            ? NoSourcesMessage("Pascal program")
            : await RunProcessAsync("fpc", ["-s", "-vewn", main]);
    }

    private string? FindProgram(bool test)
    {
        var sources = FindSourceFiles(".lpr", ".pas", ".pp").Where(file => IsTestFile(file) == test).ToList();
        return sources.FirstOrDefault(file => file.EndsWith(".lpr", StringComparison.OrdinalIgnoreCase))
            ?? sources.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Equals(test ? "test" : "main", StringComparison.OrdinalIgnoreCase))
            ?? sources.FirstOrDefault(IsProgram);
    }

    private bool IsProgram(string relativePath)
    {
        try
        {
            return ProgramRegex().IsMatch(File.ReadAllText(Path.Combine(Root, relativePath)));
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string ExecutableFor(string source) =>
        ExecutableName(Path.ChangeExtension(source, null));

    [GeneratedRegex(@"^\s*program\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ProgramRegex();
}
