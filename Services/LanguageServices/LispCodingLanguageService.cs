using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

// Common Lisp through SBCL. Validation loads main.lisp (or every non-test source); tests run test.lisp/tests.lisp
// or the files under test/ or tests/ as scripts.
public class LispCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private static readonly string[] Extensions = [".lisp", ".lsp", ".cl"];

    public async Task<string> BuildAsync() =>
        "Common Lisp has no separate build step; loaded the sources to check them.\n" + await ValidateSyntaxAsync();

    public async Task<string> RunTestsAsync()
    {
        var tests = FindSourceFiles(Extensions).Where(IsTestFile).ToList();
        if (tests.Count == 0)
            return "No Lisp tests found. Add test.lisp or files under tests/ that exit with a non-zero code on failure.";

        var results = new List<string>();
        foreach (var test in tests)
        {
            var result = await RunProcessAsync("sbcl", ["--script", test]);
            results.Add(result);
            if (!Succeeded(result))
                break;
        }

        return String.Join("\n\n", results);
    }

    public async Task<string> ValidateSyntaxAsync()
    {
        var sources = FindSourceFiles(Extensions).Where(file => !IsTestFile(file)).ToList();
        if (sources.Count == 0)
            return NoSourcesMessage("Lisp");

        var main = sources.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Equals("main", StringComparison.OrdinalIgnoreCase));
        List<string> arguments = ["--non-interactive", "--no-userinit"];
        foreach (var source in main is null ? sources : [main])
            arguments.AddRange(["--load", source]);

        return await RunProcessAsync("sbcl", arguments);
    }
}
