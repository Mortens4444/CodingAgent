using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

internal class CSharpCodingLanguageService(string root, string projectFile) : LanguageService(root), ICodingLanguageService
{
    private readonly string projectFile = projectFile;

    public Task<string> BuildAsync()
    {
        return RunDotnetAsync("build");
    }

    public Task<string> RunTestsAsync()
    {
        return RunDotnetAsync("test");
    }

    public Task<string> ValidateSyntaxAsync()
    {
        return Task.FromResult(String.Empty);
    }

    private Task<string> RunDotnetAsync(string command)
    {
        var args = new List<string> { command };
        if (!String.IsNullOrWhiteSpace(projectFile))
            args.Add(projectFile);

        return RunProcessAsync("dotnet", Root, args, $"dotnet {command} {projectFile}".Trim());
    }
}
