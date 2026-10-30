using CodingAgent.Extensions;
using CodingAgent.Interfaces;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodingAgent.Services;

public sealed class CodingAgentLoop(IChatClient chatClient, ToolDispatcher toolDispatcher)
{
    private readonly IChatClient chatClient = chatClient;
    private readonly ToolDispatcher toolDispatcher = toolDispatcher;

    public async Task RunAsync(
        ICodingLanguageService languageService,
        List<ChatMessage> messages)
    {
        for (var step = 1; step <= Constants.MaxSteps; step++)
        {
            string modelOutput;

            try
            {
                var response = await chatClient.GetResponseAsync(messages);
                modelOutput = response.Text?.Trim() ?? String.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when calling Ollama: {ex.Message}");
                return;
            }

            Console.WriteLine($"\n[Agent {step}/{Constants.MaxSteps}]\n{modelOutput}\n");
            messages.Add(new ChatMessage(ChatRole.Assistant, modelOutput));

            if (!ToolCallParser.TryParseAction(modelOutput, out var action, out var parseError))
            {
                messages.Add(new ChatMessage(
                    ChatRole.User,
                    $"HOST ERROR: {parseError}\n" +
                    "Return exactly one valid JSON object in the required format. No markdown or extra text."));

                continue;
            }

            var toolName = ToolCatalog.NormalizeToolName(action.Name);

            if (!ToolCatalog.IsValid(toolName))
            {
                var error = $"HOST ERROR: '{action.Name}' is not an existing or valid tool name. " +
                            $"Supported tools: {ToolCatalog.SupportedToolsText}";

                Console.WriteLine($"\n[Host filter] {error}\n");

                messages.Add(new ChatMessage(
                    ChatRole.User,
                    error + "\nCorrect the call and return exactly one valid JSON object using one of the permitted tools."));

                continue;
            }

            if (toolName.Equals("finish", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("=== Agent summary ===");
                Console.WriteLine(GetSummary(action.Arguments));
                return;
            }

            var toolResult = await ExecuteToolAsync(
                languageService,
                toolName,
                action.Arguments);

            messages.Add(AgentPromptBuilder.CreateToolResultMessage(toolName, toolResult));

            TrimMessages(messages);
        }

        Console.WriteLine(
            $"Agent stopped. Maximum step count: {Constants.MaxSteps}; check the results above.");
    }

    private async Task<string> ExecuteToolAsync(
        ICodingLanguageService languageService,
        string toolName,
        JsonElement arguments)
    {
        string result;

        try
        {
            result = await toolDispatcher.ExecuteToolAsync(
                languageService,
                toolName,
                arguments);
        }
        catch (Exception ex)
        {
            result = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }

        Console.WriteLine($"[Tool result: {toolName}]\n{result.Limit(Constants.MaxLength)}");

        return result;
    }

    private static string? GetSummary(JsonElement arguments)
    {
        return arguments.TryGetProperty("summary", out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void TrimMessages(List<ChatMessage> messages)
    {
        if (messages.Count > 22)
            messages.RemoveRange(2, messages.Count - 14);
    }
}