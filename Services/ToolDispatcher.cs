using CodingAgent.Commands;
using CodingAgent.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CodingAgent.Services;

public class ToolDispatcher
{
    private readonly Dictionary<string, IToolCommand> commands;

    public ToolDispatcher(IConfiguration configuration, IProcessRunner? processRunner = null)
    {
        commands = new Dictionary<string, IToolCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["list_files"] = new ListFilesCommand(),
            ["check_for_missing_files"] = new CheckForMissingFilesCommand(),
            ["read_file"] = new ReadFileCommand(),
            ["search_text"] = new SearchTextCommand(),
            ["write_file"] = new WriteFileCommand(configuration),
            ["replace_in_file"] = new ReplaceInFileCommand(),
            ["delete_file"] = new DeleteFileCommand(),
            ["move_file"] = new MoveFileCommand(),
            ["build_project"] = new BuildProjectCommand(),
            ["check_syntax"] = new CheckSyntaxCommand(),
            ["run_tests"] = new RunTestsCommand(),
            ["run_command"] = new RunCommandCommand(configuration, processRunner)
        };
    }

    // Expects a name already normalized by ToolCatalog, which owns the alias table.
    public async Task<string> ExecuteToolAsync(ICodingLanguageService codingLanguageService, string toolName, JsonElement args)
    {
        if (!commands.TryGetValue(toolName, out var command))
            return $"ERROR: Unknown tool '{toolName}'.";

        ConsoleOutput.WriteLine($"[ToolDispatcher] Executing: {toolName}", ConsoleOutput.Tool);

        // The loop prints the result, so it is not written twice.
        return await command.ExecuteAsync(args, codingLanguageService);
    }
}
