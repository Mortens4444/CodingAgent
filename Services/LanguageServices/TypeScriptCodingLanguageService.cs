using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class TypeScriptCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public Task<string> BuildAsync() =>
        File.Exists(Path.Combine(Root, "tsconfig.json"))
            ? RunProcessAsync("npx", Root, ["tsc"], "npx tsc")
            : RunProcessAsync("npm", Root, ["run", "build"], "npm run build");

    public Task<string> RunTestsAsync() =>
        File.Exists(Path.Combine(Root, "package.json"))
            ? RunProcessAsync("npm", Root, ["test"], "npm test")
            : Task.FromResult("No test script configured.");

    public Task<string> ValidateSyntaxAsync() =>
        RunProcessAsync("npx", Root, ["tsc", "--noEmit"], "npx tsc --noEmit");
}