using CodingAgent.Dto;
using CodingAgent.Enums;

namespace CodingAgent.Services;

public class ProjectITypedentifier
{
    public static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "node_modules", ".codingagent-backups",
        ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache",
        ".tox", "dist", "build"
    };

    public ResolvedProject ResolveProject(string target)
    {
        var fullTarget = Path.GetFullPath(target);
        if (File.Exists(fullTarget))
        {
            var extension = Path.GetExtension(fullTarget);
            var fileName = Path.GetFileName(fullTarget);
            var parent = Path.GetDirectoryName(fullTarget)!;

            if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return new ResolvedProject(parent, fileName, ProgrammingLanguage.CSharp);
            }

            if (fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("setup.cfg", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("Pipfile", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".pyw", StringComparison.OrdinalIgnoreCase))
            {
                return new ResolvedProject(parent, String.Empty, ProgrammingLanguage.Python);
            }

            throw new InvalidOperationException(
                "You can specify an .sln, .slnx, or .csproj file, a Python project description file, or a .py file. Otherwise, specify the project folder.");
        }

        if (!Directory.Exists(fullTarget))
            throw new DirectoryNotFoundException($"The specified route cannot be found: {fullTarget}");

        var solutions = Directory.GetFiles(fullTarget, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(fullTarget, "*.slnx", SearchOption.TopDirectoryOnly))
            .ToArray();

        if (solutions.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple solution files found. Please specify the path of the .sln/.slnx file you want to use:" +
                String.Join(", ", solutions.Select(Path.GetFileName)));
        }

        if (solutions.Length == 1)
            return new ResolvedProject(fullTarget, Path.GetFileName(solutions[0]), ProgrammingLanguage.CSharp);

        var projectKind = DetectProjectKind(fullTarget, out var projectFile);
        return new ResolvedProject(fullTarget, projectFile, projectKind);
    }

    private static ProgrammingLanguage DetectProjectKind(string root, out string projectFile)
    {
        projectFile = String.Empty;
        var pending = new Stack<string>();
        pending.Push(root);
        var pythonFile = false;
        string? csproj = null;

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var name = Path.GetFileName(file);
                    var extension = Path.GetExtension(file);
                    if (name.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("setup.cfg", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Pipfile", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".pyw", StringComparison.OrdinalIgnoreCase))
                    {
                        pythonFile = true;
                    }

                    if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) && csproj is null)
                        csproj = Path.GetRelativePath(root, file);
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    if (!ExcludedDirectories.Contains(Path.GetFileName(directory)))
                        pending.Push(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        if (pythonFile)
            return ProgrammingLanguage.Python;

        if (csproj is not null)
        {
            projectFile = csproj;
            return ProgrammingLanguage.CSharp;
        }

        return ProgrammingLanguage.Unknown;
    }
}
