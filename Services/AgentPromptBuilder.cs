using CodingAgent.Dto;
using CodingAgent.Extensions;
using Microsoft.Extensions.AI;

namespace CodingAgent.Services;

public static class AgentPromptBuilder
{
    public static List<ChatMessage> CreateInitialMessages(ProjectContext project)
    {
        return
        [
            new(ChatRole.System, PromptProvider.SystemPrompt),
            new(ChatRole.User,
                $"Project root: {project.Root}\n" +
                $"Project kind: {project.ProgrammingLanguage}\n" +
                $"Project file: {(String.IsNullOrWhiteSpace(project.ProjectFile) ? "(none)" : project.ProjectFile)}\n\n" +
                "ToDo.txt content follows. Treat it as the requested work. " +
                "Start by inspecting the project structure and relevant configuration files.\n" +
                "--- BEGIN TODO ---\n" +
                project.TodoText +
                "\n--- END TODO ---\n" +
                "Return one JSON tool call only.")
        ];
    }

    public static ChatMessage CreateToolResultMessage(
        string toolName,
        string toolResult)
    {
        return new ChatMessage(
            ChatRole.User,
            $"Tool result for '{toolName}':\n{toolResult.Limit(8000)}\n\n" +
            "Use the result above. Do not repeat the same action unless something has changed. " +
            "Return one JSON tool call only.");
    }
}