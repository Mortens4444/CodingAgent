using CodingAgent.Extensions;
using CodingAgent.Services;
using System.Text.Json;

namespace CodingAgent.Commands;

internal class Command
{
    protected static string GetString(JsonElement element, string propertyName, string defaultValue = "")
    {
        return element.GetString(propertyName, defaultValue);
    }

    protected static string ResolveWithinRoot(string root, string relativePath)
    {
        if (String.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A relative path is required.");
        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("Absolute paths are not allowed. Use a path relative to the solution root.");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Access outside the solution root is not allowed.");

        var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(ProjectITypedentifier.ExcludedDirectories.Contains))
            throw new InvalidOperationException("Paths inside build-output or internal folders (bin, obj, .git, .vs, node_modules, backups) are blocked.");

        return fullPath;
    }

    protected static string ToRelativePath(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
