using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class JavaScriptCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public Task<string> BuildAsync() =>
        File.Exists(Path.Combine(Root, "package.json"))
            ? RunProcessAsync("npm", Root, ["run", "build"], "npm run build")
            : Task.FromResult("No package.json build script configured.");

    public Task<string> RunTestsAsync() =>
        File.Exists(Path.Combine(Root, "package.json"))
            ? RunProcessAsync("npm", Root, ["test"], "npm test")
            : Task.FromResult("No package.json found for tests.");

    public Task<string> ValidateSyntaxAsync() =>
        RunProcessAsync("node", Root, ["--check", "*.js"], "node --check *.js");
}
