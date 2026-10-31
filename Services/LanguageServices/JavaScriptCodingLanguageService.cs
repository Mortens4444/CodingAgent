using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

// Uses package.json scripts when present; otherwise Node's own syntax check and built-in test runner.
public class JavaScriptCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    public async Task<string> BuildAsync() =>
        NodeProject.HasScript(Root, "build")
            ? await RunProcessAsync("npm", ["run", "build"])
            : "No build script in package.json; checked syntax instead.\n" + await ValidateSyntaxAsync();

    public Task<string> RunTestsAsync() =>
        NodeProject.HasScript(Root, "test")
            ? RunProcessAsync("npm", ["test"])
            : RunProcessAsync("node", ["--test"]);

    public async Task<string> ValidateSyntaxAsync()
    {
        var sources = FindSourceFiles(".js", ".mjs", ".cjs");
        if (sources.Count == 0)
            return NoSourcesMessage("JavaScript");

        var failures = new List<string>();
        foreach (var source in sources)
        {
            var result = await RunProcessAsync("node", ["--check", source]);
            if (!Succeeded(result))
                failures.Add(result);
        }

        return failures.Count == 0
            ? $"Command: node --check <each file>\nExit code: 0\nSyntax OK. Checked {sources.Count} JavaScript files."
            : $"Syntax errors in {failures.Count} of {sources.Count} JavaScript files:\n" + String.Join("\n\n", failures);
    }
}
