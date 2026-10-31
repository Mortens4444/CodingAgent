using CodingAgent.Enums;
using CodingAgent.Interfaces;
using System.Text;

namespace CodingAgent.Services.DevEnvironment;

public sealed record DevEnvironmentReport(string Text, IReadOnlyList<ToolRequirement> MissingRequired, IReadOnlyList<ToolRequirement> MissingOptional)
{
    public bool IsReady => MissingRequired.Count == 0;

    // A short note for the model so it does not keep retrying builds that cannot run.
    public string? PromptNote => IsReady
        ? null
        : "Missing required tools on this machine: " + String.Join(", ", MissingRequired.Select(tool => tool.Name)) +
          ". build_project/run_tests steps that need them will fail; mention this in the finish summary.";
}

public static class DevEnvironmentChecker
{
    public static DevEnvironmentReport Check(ProgrammingLanguage language, IProcessRunner processRunner)
    {
        var tools = ToolchainCatalog.For(language);
        var text = new StringBuilder($"Development environment for {language}:\n");
        var missingRequired = new List<ToolRequirement>();
        var missingOptional = new List<ToolRequirement>();

        if (tools.Count == 0)
            text.AppendLine("  (no toolchain requirements known)");

        foreach (var tool in tools)
        {
            var path = tool.Commands.Select(processRunner.FindExecutable).FirstOrDefault(found => found is not null);
            if (path is not null)
            {
                text.AppendLine($"  {"[OK]",-10} {tool.Name,-12} {path}");
                continue;
            }

            (tool.Optional ? missingOptional : missingRequired).Add(tool);
            text.AppendLine($"  {(tool.Optional ? "[OPTIONAL]" : "[MISSING]"),-10} {tool.Name,-12} {tool.Purpose}. Install: {tool.InstallHint}");
        }

        if (missingRequired.Count > 0 || missingOptional.Count > 0)
            text.AppendLine($"  Run {ToolchainCatalog.SetupScript} -Languages {language} to install the missing tools, then restart the terminal.");

        return new DevEnvironmentReport(text.ToString().TrimEnd(), missingRequired, missingOptional);
    }
}
