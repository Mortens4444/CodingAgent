using CodingAgent.Dto;
using CodingAgent.Extensions;
using Microsoft.Extensions.AI;

namespace CodingAgent.Services;

public static class AgentPromptBuilder
{
    public static List<ChatMessage> CreateInitialMessages(ProjectContext project, string? environmentNote = null)
    {
        return
        [
            new(ChatRole.System, PromptProvider.SystemPrompt),
            new(ChatRole.User,
                $"Project root: {project.Root}\n" +
                $"Project kind: {project.ProgrammingLanguage}\n" +
                $"Project file: {(String.IsNullOrWhiteSpace(project.ProjectFile) ? "(none)" : project.ProjectFile)}\n" +
                (String.IsNullOrWhiteSpace(environmentNote) ? String.Empty : environmentNote + "\n") +
                "\n" +
                "ToDo.txt content follows. Treat it as the requested work. " +
                "Inspect only what you need to locate the change, then implement it.\n" +
                "--- BEGIN TODO ---\n" +
                project.TodoText +
                "\n--- END TODO ---\n" +
                "Return one JSON tool call only.")
        ];
    }

    // hostNote is placed after the (possibly shortened) result, so condensing never cuts it off.
    // The trailer stays short: it is repeated in every result kept in the history, and the response
    // format already forces a single JSON call while the loop refuses repeated identical calls.
    public static ChatMessage CreateToolResultMessage(
        string toolName,
        string toolResult,
        string? hostNote = null)
    {
        return new ChatMessage(
            ChatRole.User,
            $"Tool result for '{toolName}':\n{ToolOutputCondenser.Condense(toolResult, Constants.MaxToolResultLength)}\n\n" +
            (String.IsNullOrWhiteSpace(hostNote) ? String.Empty : hostNote + "\n") +
            "Use this result; return the next JSON tool call.");
    }
}