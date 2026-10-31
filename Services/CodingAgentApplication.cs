using CodingAgent.Dto;
using CodingAgent.Enums;
using CodingAgent.Interfaces;
using CodingAgent.Services.DevEnvironment;
using CodingAgent.Services.LanguageServices;

namespace CodingAgent.Services;

public sealed class CodingAgentApplication(ProjectContextLoader projectContextLoader, CodingAgentLoop agentLoop, IProcessRunner? processRunner = null)
{
    private readonly ProjectContextLoader projectContextLoader = projectContextLoader;
    private readonly CodingAgentLoop agentLoop = agentLoop;
    private readonly IProcessRunner processRunner = processRunner ?? ProcessRunner.Default;

    public async Task RunAsync()
    {
        var project = await projectContextLoader.LoadAsync();
        if (project is null) return;

        var languageService = CreateLanguageService(project, processRunner);

        var environment = DevEnvironmentChecker.Check(project.ProgrammingLanguage, processRunner);
        ConsoleOutput.WriteLine(environment.Text + Environment.NewLine, environment.IsReady ? ConsoleOutput.Tool : ConsoleOutput.Warning);

        var messages = AgentPromptBuilder.CreateInitialMessages(project, environment.PromptNote);

        await agentLoop.RunAsync(languageService, messages);
    }

    public static ICodingLanguageService CreateLanguageService(ProjectContext project, IProcessRunner processRunner) =>
            project.ProgrammingLanguage switch
            {
                ProgrammingLanguage.CSharp => new CSharpCodingLanguageService(project.Root, project.ProjectFile, processRunner),
                ProgrammingLanguage.Python => new PythonCodingLanguagetService(project.Root, processRunner),
                ProgrammingLanguage.C => new CCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.Cpp => new CppCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.Java => new JavaCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.JavaScript => new JavaScriptCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.TypeScript => new TypeScriptCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.Html => new HtmlCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.Assembly => new AssemblyCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.Pascal => new PascalCodingLanguageService(project.Root, processRunner),
                ProgrammingLanguage.Lisp => new LispCodingLanguageService(project.Root, processRunner),
                _ => throw new NotSupportedException($"The programming language '{project.ProgrammingLanguage}' is not supported.")
            };
}