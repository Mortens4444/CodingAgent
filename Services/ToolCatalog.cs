namespace CodingAgent.Services;

public static class ToolCatalog
{
    private static readonly string[] supportedToolNames =
    [
        "list_files",
        "search_text",
        "read_file",
        "write_file",
        "check_for_missing_files",
        "build_project",
        "run_tests",
        "finish"
    ];

    private static readonly HashSet<string> validTools = new(supportedToolNames, StringComparer.OrdinalIgnoreCase);

    public static string SupportedToolsText => String.Join(", ", supportedToolNames);

    public static bool IsValid(string name) => validTools.Contains(name);

    public static string NormalizeToolName(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "list_files" or "list_directory" or "list_dir" => "list_files",
            "search_text" or "search_files" => "search_text",
            "read_file" or "get_file_contents" => "read_file",
            "write_file" or "edit_file" or "update_file" => "write_file",
            "check_file_exists" or "check_file" or "file_exists" => "check_for_missing_files",
            "check_for_missing_files" or "check_missing_files" => "check_for_missing_files",
            "build_solution" or "build_project" => "build_project",
            "run_tests" or "test_project" => "run_tests",
            "finish" or "complete" => "finish",

            _ => name.Trim()
        };
    }
}
