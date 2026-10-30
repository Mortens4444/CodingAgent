namespace CodingAgent;

public static  class Constants
{
    public const int MaxLength = 120000;

    public const int MaxSteps = 100;

    public static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "node_modules", ".codingagent-backups",
        ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache",
        ".tox", "dist", "build"
    };
}
