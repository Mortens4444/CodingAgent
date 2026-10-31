using CodingAgent.Extensions;
using System.Text.Json;

namespace CodingAgent.Commands;

public class Command
{
    public static readonly string[] PathNames = ["path", "file_path"];

    protected static string GetString(JsonElement element, string propertyName, string defaultValue = "")
    {
        return element.GetString(propertyName, defaultValue);
    }

    // Models use many spellings for the same argument (old_str, old_text, search, ...).
    protected static string GetFirstString(JsonElement element, params string[] propertyNames) =>
        element.GetFirstString(propertyNames) ?? String.Empty;

    protected static bool HasString(JsonElement element, params string[] propertyNames) =>
        element.ValueKind == JsonValueKind.Object &&
        propertyNames.Any(name => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String);

    protected static bool GetBool(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True ||
         (value.ValueKind == JsonValueKind.String && Boolean.TryParse(value.GetString(), out var parsed) && parsed));

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

        if (parts.Any(Constants.ExcludedDirectories.Contains))
            throw new InvalidOperationException("Paths inside build-output or public folders (bin, obj, .git, .vs, node_modules, backups) are blocked.");

        return fullPath;
    }

    protected static string ToRelativePath(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
