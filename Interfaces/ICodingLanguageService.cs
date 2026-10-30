namespace CodingAgent.Interfaces;

public interface ICodingLanguageService
{
    string Root { get; }

    Task<string> BuildAsync();

    Task<string> RunTestsAsync();

    Task<string> ValidateSyntaxAsync();
}
