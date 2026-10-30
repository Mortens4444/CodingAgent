using CodingAgent.Dto;
using CodingAgent.Enums;
using CodingAgent.Interfaces;
using CodingAgent.Services.LanguageServices;

namespace CodingAgent.Services;

public sealed class CodingAgentApplication(ProjectContextLoader projectContextLoader, CodingAgentLoop agentLoop)
{
    private readonly ProjectContextLoader projectContextLoader = projectContextLoader;
    private readonly CodingAgentLoop agentLoop = agentLoop;

    public async Task RunAsync()
    {
        var project = await projectContextLoader.LoadAsync();
        if (project is null) return;

        var languageService = CreateLanguageService(project);
        var messages = AgentPromptBuilder.CreateInitialMessages(project);

        await agentLoop.RunAsync(languageService, messages);
    }

    private static ICodingLanguageService CreateLanguageService(ProjectContext project) =>
        project.ProgrammingLanguage switch
        {
            ProgrammingLanguage.CSharp => new CSharpCodingLanguageService(project.Root, project.ProjectFile),
            ProgrammingLanguage.Python => new PythonCodingLanguagetService(project.Root),
            _ => throw new NotSupportedException($"The programming language is not supported.")
        };
}