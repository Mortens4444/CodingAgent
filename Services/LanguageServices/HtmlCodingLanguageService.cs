using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class HtmlCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public Task<string> BuildAsync() => Task.FromResult("HTML is a markup language and does not require building.");

    public Task<string> RunTestsAsync() => Task.FromResult("No automated tests configured for HTML.");

    public Task<string> ValidateSyntaxAsync() =>
        File.Exists(Path.Combine(Root, ".htmlhintrc")) || Directory.Exists(Path.Combine(Root, "node_modules"))
            ? RunProcessAsync("npx", Root, ["htmlhint", "."], "npx htmlhint .")
            : Task.FromResult("HTML syntax check skipped (htmlhint not configured).");
}