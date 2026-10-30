using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class LispCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public Task<string> BuildAsync() => Task.FromResult("Common Lisp is interpreted/evaluated dynamically.");

    public Task<string> RunTestsAsync() => RunProcessAsync("sbcl", Root, ["--script", "test.lisp"], "sbcl --script test.lisp");

    public Task<string> ValidateSyntaxAsync() => RunProcessAsync("sbcl", Root, ["--non-interactive", "--eval", "(load \"main.lisp\")"], "sbcl --non-interactive --eval '(load \"main.lisp\")'");
}