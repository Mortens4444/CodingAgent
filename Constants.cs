namespace CodingAgent;

public static  class Constants
{
    public const int MaxLength = 120000;

    public const int MaxSteps = 100;

    // Ollama's own default is 4096, which truncates the system prompt and task.
    public const int DefaultContextLength = 8192;

    // Largest tool result shown to the model; read_file pages stay below it.
    public const int MaxToolResultLength = 8000;

    public const int ReadFilePageLength = 6000;

    public static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "node_modules", ".codingagent-backups",
        ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache",
        ".tox", "dist", "build"
    };
}
