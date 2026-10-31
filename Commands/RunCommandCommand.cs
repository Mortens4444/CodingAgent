using CodingAgent.Interfaces;
using CodingAgent.Services;
using CodingAgent.Services.DevEnvironment;
using CodingAgent.Services.LanguageServices;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

// Runs one program in the project root without a shell (no pipes, redirects or &&).
// CodingAgent:RunCommand:Approval decides who allows it: Ask (default, the user confirms each command), Always, or Never.
public class RunCommandCommand(IConfiguration configuration, IProcessRunner? processRunner = null) : Command, IToolCommand
{
    private readonly string approval = configuration.GetValue("CodingAgent:RunCommand:Approval", "Ask")!;
    private readonly IProcessRunner processRunner = processRunner ?? ProcessRunner.Default;

    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var (executable, arguments) = GetCommand(args);
        if (String.IsNullOrWhiteSpace(executable))
            return "ERROR: run_command requires 'command', for example {\"command\":\"npm\",\"args\":[\"install\"]}.";

        var display = String.Join(" ", new[] { executable }.Concat(arguments).Select(part => part.Contains(' ') ? $"\"{part}\"" : part));

        if (approval.Equals("Never", StringComparison.OrdinalIgnoreCase))
            return "ERROR: run_command is disabled by configuration (CodingAgent:RunCommand:Approval=Never). Use the other tools.";

        if (!approval.Equals("Always", StringComparison.OrdinalIgnoreCase) && !AskUser(display))
            return $"DENIED: The user did not allow '{display}'. Do not ask for it again; continue with other tools or explain the blocker in finish.";

        try
        {
            return await processRunner.RunAsync(executable, codingLanguageService.Root, arguments, display);
        }
        catch (Win32Exception)
        {
            return ToolchainCatalog.MissingToolMessage(executable);
        }
    }

    private static bool AskUser(string display)
    {
        ConsoleOutput.WriteLine($"\nThe agent wants to run: {display}", ConsoleOutput.Warning);
        return UserApproval.Ask("Allow?");
    }

    // Accepts {"command":"npm","args":["install"]} or a whole command line in "command".
    private static (string Executable, List<string> Arguments) GetCommand(JsonElement args)
    {
        var command = GetFirstString(args, "command", "cmd", "program");
        var arguments = new List<string>();

        if (args.ValueKind == JsonValueKind.Object &&
            (args.TryGetProperty("args", out var list) || args.TryGetProperty("arguments", out list)) &&
            list.ValueKind == JsonValueKind.Array)
        {
            arguments.AddRange(list.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()! : item.GetRawText()));
            return (command.Trim(), arguments);
        }

        var parts = SplitCommandLine(command);
        return parts.Count == 0 ? (String.Empty, arguments) : (parts[0], parts.Skip(1).ToList());
    }

    public static List<string> SplitCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var inPart = false;

        foreach (var c in commandLine)
        {
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                else current.Append(c);
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                inPart = true;
            }
            else if (Char.IsWhiteSpace(c))
            {
                if (inPart) parts.Add(current.ToString());
                current.Clear();
                inPart = false;
            }
            else
            {
                current.Append(c);
                inPart = true;
            }
        }

        if (inPart) parts.Add(current.ToString());
        return parts;
    }
}
