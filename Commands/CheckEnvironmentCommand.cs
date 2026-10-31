using CodingAgent.Enums;
using CodingAgent.Interfaces;
using CodingAgent.Services.DevEnvironment;
using CodingAgent.Services.LanguageServices;
using System.Text.Json;

namespace CodingAgent.Commands;

// Re-checks the toolchain, for example after the user installed a missing compiler while the agent was running.
// The PATH is refreshed first, so newly installed tools are found without restarting the agent.
public class CheckEnvironmentCommand(IProcessRunner? processRunner = null) : Command, IToolCommand
{
    private readonly IProcessRunner processRunner = processRunner ?? ProcessRunner.Default;

    public Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var added = DevEnvironmentChecker.RefreshPath();
        var report = DevEnvironmentChecker.Check(LanguageOf(codingLanguageService), processRunner);

        var result = report.Text +
                     (added > 0 ? $"\n(PATH refreshed: {added} new director{(added == 1 ? "y" : "ies")} found.)" : String.Empty) +
                     (report.IsReady
                         ? "\nENVIRONMENT READY: all required tools are installed; build_project and run_tests can run."
                         : "\nENVIRONMENT INCOMPLETE: " + report.PromptNote);

        return Task.FromResult(result);
    }

    public static ProgrammingLanguage LanguageOf(ICodingLanguageService service) => service switch
    {
        CSharpCodingLanguageService => ProgrammingLanguage.CSharp,
        PythonCodingLanguagetService => ProgrammingLanguage.Python,
        CppCodingLanguageService => ProgrammingLanguage.Cpp,
        CCodingLanguageService => ProgrammingLanguage.C,
        JavaCodingLanguageService => ProgrammingLanguage.Java,
        TypeScriptCodingLanguageService => ProgrammingLanguage.TypeScript,
        JavaScriptCodingLanguageService => ProgrammingLanguage.JavaScript,
        HtmlCodingLanguageService => ProgrammingLanguage.Html,
        AssemblyCodingLanguageService => ProgrammingLanguage.Assembly,
        PascalCodingLanguageService => ProgrammingLanguage.Pascal,
        LispCodingLanguageService => ProgrammingLanguage.Lisp,
        _ => ProgrammingLanguage.Unknown
    };
}
