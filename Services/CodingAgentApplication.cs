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
                ProgrammingLanguage.C => new CCodingLanguageService(project.Root),
                ProgrammingLanguage.Cpp => new CppCodingLanguageService(project.Root),
                ProgrammingLanguage.Java => new JavaCodingLanguageService(project.Root),
                ProgrammingLanguage.JavaScript => new JavaScriptCodingLanguageService(project.Root),
                ProgrammingLanguage.TypeScript => new TypeScriptCodingLanguageService(project.Root),
                ProgrammingLanguage.Html => new HtmlCodingLanguageService(project.Root),
                ProgrammingLanguage.Assembly => new AssemblyCodingLanguageService(project.Root),
                ProgrammingLanguage.Pascal => new PascalCodingLanguageService(project.Root),
                ProgrammingLanguage.Lisp => new LispCodingLanguageService(project.Root),
                _ => throw new NotSupportedException($"The programming language '{project.ProgrammingLanguage}' is not supported.")
            };
}