using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class TypeScriptCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    public Task<string> BuildAsync()
    {
        if (NodeProject.HasScript(Root, "build"))
            return RunProcessAsync("npm", ["run", "build"]);

        return HasFile("tsconfig.json") ? RunTscAsync([]) : ValidateSyntaxAsync();
    }

    public Task<string> RunTestsAsync() =>
        NodeProject.HasScript(Root, "test")
            ? RunProcessAsync("npm", ["test"])
            : RunProcessAsync("node", ["--test"]);

    public async Task<string> ValidateSyntaxAsync()
    {
        if (HasFile("tsconfig.json"))
            return await RunTscAsync(["--noEmit"]);

        var sources = FindSourceFiles(".ts", ".tsx");
        return sources.Count == 0
            ? NoSourcesMessage("TypeScript")
            : await RunTscAsync(["--noEmit", .. sources]);
    }

    // Plain "npx tsc" without a local install would fetch the unrelated "tsc" package, so name typescript explicitly.
    private Task<string> RunTscAsync(string[] arguments) =>
        NodeProject.HasLocalBinary(Root, "tsc")
            ? RunProcessAsync("npx", ["tsc", .. arguments])
            : RunProcessAsync("npx", ["--yes", "-p", "typescript", "tsc", .. arguments]);
}
