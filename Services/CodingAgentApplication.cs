using CodingAgent.Interfaces;
using CodingAgent.Services.LanguageServices;

namespace CodingAgent.Services;

internal sealed class CodingAgentApplication(ProjectContextLoader projectContextLoader, CodingAgentLoop agentLoop)
{
    private readonly ProjectContextLoader projectContextLoader = projectContextLoader;
    private readonly CodingAgentLoop agentLoop = agentLoop;

    public async Task RunAsync()
    {
        var project = await projectContextLoader.LoadAsync();

        if (project is null)
            return;

        ICodingLanguageService languageService = new CSharpCodingLanguageService(project.Root, project.ProjectFile);
        var messages = AgentPromptBuilder.CreateInitialMessages(project);
        await agentLoop.RunAsync(languageService, messages);
    }
}