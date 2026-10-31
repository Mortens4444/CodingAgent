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
            text.AppendLine($"  Run {ToolchainCatalog.SetupCommand(language)} to install the missing tools, then restart the terminal.");

        return new DevEnvironmentReport(text.ToString().TrimEnd(), missingRequired, missingOptional);
    }

    // Tools installed while the agent runs are only on the PATH stored in the registry, not on the PATH this
    // process started with. Appends the new machine and user entries, keeping the process entries and their order.
    // Returns the number of added directories. Other systems set PATH in shell profiles, which cannot be re-read here.
    public static int RefreshPath()
    {
        if (!OperatingSystem.IsWindows())
            return 0;

        var current = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
        var stored = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) + Path.PathSeparator +
                     Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);

        var (path, added) = MergePath(current, stored);
        if (added > 0)
            Environment.SetEnvironmentVariable("PATH", path);

        return added;
    }

    public static (string Path, int Added) MergePath(string current, string stored)
    {
        static string Normalize(string entry) => Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"')).TrimEnd('\\', '/');

        var entries = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var known = new HashSet<string>(entries.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var entry in stored.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (known.Add(Normalize(entry)))
            {
                entries.Add(Environment.ExpandEnvironmentVariables(entry));
                added++;
            }
        }

        return (String.Join(Path.PathSeparator, entries), added);
    }
}
