using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodingAgent.Services;

public static class ToolCatalog
{
    private static readonly string[] supportedToolNames =
    [
        "list_files",
        "search_text",
        "read_file",
        "write_file",
        "replace_in_file",
        "delete_file",
        "move_file",
        "check_for_missing_files",
        "build_project",
        "check_syntax",
        "run_tests",
        "run_command",
        "finish"
    ];

    private static readonly HashSet<string> validTools = new(supportedToolNames, StringComparer.OrdinalIgnoreCase);

    public static string SupportedToolsText => String.Join(", ", supportedToolNames);

    public static bool IsValid(string name) => validTools.Contains(name);

    // JSON schema handed to Ollama as the response format; it is enforced as a grammar while sampling.
    public static ChatResponseFormat ResponseFormat { get; } = ChatResponseFormat.ForJsonSchema(
        JsonDocument.Parse($$"""
            {
              "type": "object",
              "properties": {
                "name": { "type": "string", "enum": {{JsonSerializer.Serialize(supportedToolNames, ToolCatalogJsonContext.Default.StringArray)}} },
                "arguments": { "type": "object" }
              },
              "required": ["name", "arguments"]
            }
            """).RootElement.Clone(),
        "tool_call",
        "One call to a supported tool.");

    public static string NormalizeToolName(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "list_files" or "list_directory" or "list_dir" => "list_files",
            "search_text" or "search_files" => "search_text",
            "read_file" or "get_file_contents" or "open_file" => "read_file",
            "write_file" or "update_file" or "create_file" => "write_file",
            "replace_in_file" or "edit_file" or "str_replace" or "replace_text" or "apply_edit" => "replace_in_file",
            "delete_file" or "remove_file" => "delete_file",
            "move_file" or "rename_file" => "move_file",
            "check_syntax" or "validate_syntax" or "lint" => "check_syntax",
            "run_command" or "execute_command" or "run_shell" or "shell" => "run_command",
            "check_file_exists" or "check_file" or "file_exists" => "check_for_missing_files",
            "check_for_missing_files" or "check_missing_files" => "check_for_missing_files",
            "build_solution" or "build_project" => "build_project",
            "run_tests" or "test_project" => "run_tests",
            "finish" or "complete" => "finish",

            _ => name.Trim()
        };
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(string[]))]
internal partial class ToolCatalogJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
