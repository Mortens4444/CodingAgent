using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class CSharpCodingLanguageService(string root, string projectFile) : LanguageService(root), ICodingLanguageService
{
    private readonly string projectFile = projectFile;

    public Task<string> BuildAsync() => RunDotnetAsync("build", "--configuration", "Debug", "--no-incremental", "-warnaserror");

    public Task<string> RunTestsAsync() => RunDotnetAsync("test");

    public Task<string> ValidateSyntaxAsync() => Task.FromResult(String.Empty);

    private Task<string> RunDotnetAsync(string command, params string[] options)
    {
        var args = new List<string> { command };
        args.AddRange(options);

        if (!String.IsNullOrWhiteSpace(projectFile))
            args.Add(projectFile);

        return RunProcessAsync(
            "dotnet",
            Root,
            args,
            $"dotnet {String.Join(" ", args)}");
    }
}
