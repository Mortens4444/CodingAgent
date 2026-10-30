using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class PascalCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public Task<string> BuildAsync() => RunProcessAsync("fpc", Root, ["main.pas"], "fpc main.pas");

    public Task<string> RunTestsAsync() => RunProcessAsync("./main", Root, [], "./main");

    public Task<string> ValidateSyntaxAsync() => RunProcessAsync("fpc", Root, ["-s", "main.pas"], "fpc -s main.pas");
}