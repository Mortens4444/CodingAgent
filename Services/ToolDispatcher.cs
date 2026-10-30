using CodingAgent.Commands;
using CodingAgent.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CodingAgent.Services;

public class ToolDispatcher
{
    private readonly Dictionary<string, IToolCommand> commands;

    public ToolDispatcher(IConfiguration configuration)
    {
        commands = new Dictionary<string, IToolCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["list_files"] = new ListFilesCommand(),
            ["check_for_missing_files"] = new CheckForMissingFilesCommand(),
            ["read_file"] = new ReadFileCommand(),
            ["search_text"] = new SearchTextCommand(),
            ["write_file"] = new WriteFileCommand(configuration),
            ["build_project"] = new BuildProjectCommand(),
            ["build_solution"] = new BuildProjectCommand(), // Aliased to same command
            ["run_tests"] = new RunTestsCommand()
        };
    }

    public async Task<string> ExecuteToolAsync(ICodingLanguageService codingLanguageService, string toolName, JsonElement args)
    {
        if (!commands.TryGetValue(toolName, out var command))
            return $"ERROR: Unknown tool '{toolName}'.";

        Console.WriteLine($"[ToolDispatcher] Executing: {toolName}");

        var result = await command.ExecuteAsync(args, codingLanguageService);

        Console.WriteLine($"[ToolDispatcher] Result: {result}");

        return result;
    }
}
