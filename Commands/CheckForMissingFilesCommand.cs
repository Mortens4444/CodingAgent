using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

public class CheckForMissingFilesCommand : Command, IToolCommand
{
    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var suppliedPaths = GetPaths(args);
        if (suppliedPaths is null)
            return "ERROR: check_for_missing_files requires file_paths as an array of path strings.";

        var statuses = new List<string>();
        foreach (var suppliedPath in suppliedPaths.Take(100))
        {
            try
            {
                var file = ResolveToolPath(codingLanguageService.Root, suppliedPath);
                statuses.Add($"{(File.Exists(file) || Directory.Exists(file) ? "EXISTS" : "MISSING")}: {ToRelativePath(codingLanguageService.Root, file)}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                statuses.Add($"BLOCKED: {suppliedPath} ({ex.Message})");
            }
        }

        return String.Join(Environment.NewLine, statuses);
    }

    // Aliases such as file_exists usually send a single "path" instead of "file_paths".
    private static List<string>? GetPaths(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return null;

        if (args.TryGetProperty("file_paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
            return paths.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? String.Empty)
                .ToList();

        var single = GetString(args, "path", GetString(args, "file_path"));
        return String.IsNullOrWhiteSpace(single) ? null : [single];
    }

    private static string ResolveToolPath(string root, string suppliedPath)
    {
        if (String.IsNullOrWhiteSpace(suppliedPath))
            throw new ArgumentException("A file path is required.");

        if (!Path.IsPathRooted(suppliedPath))
            return ResolveWithinRoot(root, suppliedPath);

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(suppliedPath);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("The path is outside the project root.");

        return ResolveWithinRoot(root, relative);
    }
}
