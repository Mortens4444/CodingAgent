using CodingAgent.Extensions;
using CodingAgent.Interfaces;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodingAgent.Services;

public sealed class CodingAgentLoop(IChatClient chatClient, ToolDispatcher toolDispatcher, int contextLength = Constants.DefaultContextLength)
{
    // Rough token estimate for code and English; deliberately pessimistic.
    private const double CharsPerToken = 3.0;

    private readonly IChatClient chatClient = chatClient;
    private readonly ToolDispatcher toolDispatcher = toolDispatcher;

    // Leaves a quarter of the context window for the model's reply.
    private readonly int promptTokenBudget = contextLength * 3 / 4;

    // A model that cannot produce a usable call this many times in a row is stuck; stop instead of burning steps.
    private const int MaxConsecutiveHostErrors = 5;

    // The same call with the same arguments this many times without a file change in between is refused instead
    // of executed. Small models often alternate between two calls (read, failed edit, read, ...), so the calls
    // need not be consecutive.
    private const int MaxIdenticalCalls = 3;

    // After this many tool calls without a file change the model is reminded to start implementing. Small models
    // tend to keep listing and reading files until the step limit and never change anything.
    private const int MaxStepsWithoutChange = 8;

    public async Task RunAsync(
        ICodingLanguageService languageService,
        List<ChatMessage> messages)
    {
        var progress = new AgentProgress();
        var hostErrors = 0;
        var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var changeCountAtLastCall = 0;
        var stepsWithoutChange = 0;
        var finishObjections = new HashSet<string>(StringComparer.Ordinal);

        for (var step = 1; step <= Constants.MaxSteps; step++)
        {
            string modelOutput;

            if (hostErrors >= MaxConsecutiveHostErrors)
            {
                ConsoleOutput.WriteLine($"Agent stopped: the model returned {hostErrors} unusable responses in a row.", ConsoleOutput.Error);
                return;
            }

            // Results dropped from the history are no longer visible, so repeating those calls is legitimate again.
            if (TrimMessages(messages, progress))
                callCounts.Clear();

            try
            {
                var response = await chatClient.GetResponseAsync(messages);
                modelOutput = response.Text?.Trim() ?? String.Empty;
            }
            catch (Exception ex)
            {
                ConsoleOutput.WriteLine($"Error when calling Ollama: {ex.Message}", ConsoleOutput.Error);
                return;
            }

            ConsoleOutput.WriteLine($"\n[Agent {step}/{Constants.MaxSteps}]", ConsoleOutput.Heading);
            ConsoleOutput.WriteLine(modelOutput + "\n", ConsoleOutput.Model);
            messages.Add(new ChatMessage(ChatRole.Assistant, modelOutput));

            if (!ToolCallParser.TryParseAction(modelOutput, out var action, out var parseError))
            {
                AddHostMessage(messages,
                    $"HOST ERROR: {parseError}\n" +
                    "Return exactly one valid JSON object in the required format. No markdown or extra text.");

                hostErrors++;
                continue;
            }

            var toolName = ToolCatalog.NormalizeToolName(action.Name);

            if (!ToolCatalog.IsValid(toolName))
            {
                var error = $"HOST ERROR: '{action.Name}' is not an existing or valid tool name. " +
                            $"Supported tools: {ToolCatalog.SupportedToolsText}";

                AddHostMessage(messages,
                    error + "\nCorrect the call and return exactly one valid JSON object using one of the permitted tools.");

                hostErrors++;
                continue;
            }

            hostErrors = 0;

            if (toolName.Equals("finish", StringComparison.OrdinalIgnoreCase))
            {
                var objection = GetFinishObjection(progress, finishObjections);
                if (objection is not null)
                {
                    AddHostMessage(messages, objection);
                    continue;
                }

                ConsoleOutput.WriteLine("=== Agent summary ===", ConsoleOutput.Heading);
                ConsoleOutput.WriteLine(GetSummary(action.Arguments), ConsoleOutput.Success);
                return;
            }

            if (progress.ChangeCount != changeCountAtLastCall)
            {
                callCounts.Clear();
                changeCountAtLastCall = progress.ChangeCount;
                stepsWithoutChange = 0;
            }

            var call = toolName + " " + (action.Arguments.ValueKind == JsonValueKind.Undefined ? "{}" : action.Arguments.GetRawText());
            var identicalCalls = callCounts[call] = callCounts.GetValueOrDefault(call) + 1;

            if (identicalCalls >= MaxIdenticalCalls)
            {
                AddHostMessage(messages,
                    $"HOST CHECK: You called {toolName} with the same arguments {identicalCalls} times since the last file change; " +
                    "it was not run again because the result would be the same. Use the earlier result and choose a different action.");
                continue;
            }

            var toolResult = await ExecuteToolAsync(
                languageService,
                toolName,
                action.Arguments);

            progress.Record(toolName, action.Arguments, toolResult);

            string? hostNote = null;
            if (progress.ChangeCount == changeCountAtLastCall && ++stepsWithoutChange % MaxStepsWithoutChange == 0)
            {
                hostNote = progress.ChangeCount == 0
                    ? $"HOST NOTE: {stepsWithoutChange} tool calls and no file has been changed yet. You have looked around enough: " +
                      "start implementing ToDo.txt now with replace_in_file or write_file."
                    : $"HOST NOTE: {stepsWithoutChange} tool calls since the last file change. " +
                      "Make the next change now (replace_in_file or write_file), or call finish if everything is done and validated.";
                ConsoleOutput.WriteLine($"[Host] {hostNote}\n", ConsoleOutput.Warning);
            }

            messages.Add(AgentPromptBuilder.CreateToolResultMessage(toolName, toolResult, hostNote));
        }

        ConsoleOutput.WriteLine(
            $"Agent stopped. Maximum step count: {Constants.MaxSteps}; check the results above.", ConsoleOutput.Warning);
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

        ConsoleOutput.WriteLine($"[Tool result: {toolName}]", ConsoleOutput.Heading);
        ConsoleOutput.WriteLine(result.Limit(Constants.MaxLength), ConsoleOutput.ForToolResult(result));

        return result;
    }

    // Each objection is raised once only, so a genuine blocker (or a task that needs no change) can still end the run.
    private static string? GetFinishObjection(AgentProgress progress, HashSet<string> raised)
    {
        if (progress.ChangeCount == 0 && raised.Add("no-change"))
            return "HOST CHECK: No file has been changed, so nothing from ToDo.txt is implemented yet. " +
                   "Inspecting files or describing a plan does not complete the task. Make the required changes with " +
                   "replace_in_file or write_file, validate them, then call finish. Only if ToDo.txt really needs no change, " +
                   "call finish again and explain why in the summary.";

        if (progress.HasUnvalidatedChanges && raised.Add("unvalidated"))
            return "HOST CHECK: Files were changed after the last build_project/run_tests, so the changes are not validated. " +
                   "Run build_project (and run_tests when tests exist), fix any failures, then call finish again.";

        if (progress.LastValidationFailed && raised.Add("failed"))
            return "HOST CHECK: The last build_project/check_syntax/run_tests did not pass. " +
                   "Fix the reported errors and validate again. If it cannot be fixed (for example a missing toolchain), " +
                   "call finish again and state the failure in the summary.";

        return null;
    }

    // Host corrections go to the model as user messages; they are shown on the console as well.
    private static void AddHostMessage(List<ChatMessage> messages, string text)
    {
        ConsoleOutput.WriteLine($"[Host] {text}\n", ConsoleOutput.Warning);
        messages.Add(new ChatMessage(ChatRole.User, text));
    }

    private static string? GetSummary(JsonElement arguments)
    {
        return arguments.TryGetProperty("summary", out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    // Keeps the system prompt and the task (the first two messages) and drops the oldest
    // history until the prompt fits the context window, so Ollama never truncates it itself.
    // When something was dropped, a progress note right after the task keeps the essentials.
    // A model reply and the tool result answering it are dropped together, so roles keep alternating.
    // Returns whether anything was dropped.
    private bool TrimMessages(List<ChatMessage> messages, AgentProgress progress)
    {
        var hasNote = messages.Count > 2 && messages[2].Text?.StartsWith(AgentProgress.NotePrefix, StringComparison.Ordinal) == true;
        var firstHistory = hasNote ? 3 : 2;
        var trimmed = false;
        var characters = messages.Sum(MessageCharacters);

        while (messages.Count > firstHistory + 1 && (int)(characters / CharsPerToken) > promptTokenBudget)
        {
            characters -= MessageCharacters(messages[firstHistory]);
            messages.RemoveAt(firstHistory);
            if (messages.Count > firstHistory + 1 && messages[firstHistory].Role == ChatRole.User)
            {
                characters -= MessageCharacters(messages[firstHistory]);
                messages.RemoveAt(firstHistory);
            }
            trimmed = true;
        }

        if ((trimmed || hasNote) && !progress.IsEmpty)
        {
            var note = new ChatMessage(ChatRole.User, progress.ToNote());
            if (hasNote) messages[2] = note;
            else messages.Insert(2, note);
        }

        return trimmed;
    }

    // Text length plus a small per-message overhead for the role and template tokens.
    private static int MessageCharacters(ChatMessage message) => (message.Text?.Length ?? 0) + 8;
}