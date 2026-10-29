using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

internal class CheckForMissingFilesCommand : Command, IToolCommand
{
    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        if (!args.TryGetProperty("file_paths", out var paths) || paths.ValueKind != JsonValueKind.Array)
            return "ERROR: check_for_missing_files requires file_paths as an array of path strings.";

        var statuses = new List<string>();
        foreach (var item in paths.EnumerateArray().Take(100))
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var suppliedPath = item.GetString() ?? String.Empty;
            try
            {
                var file = ResolveToolPath(codingLanguageService.Root, suppliedPath, allowAbsolute: true);
                statuses.Add($"{(File.Exists(file) || Directory.Exists(file) ? "EXISTS" : "MISSING")}: {ToRelativePath(codingLanguageService.Root, file)}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                statuses.Add($"BLOCKED: {suppliedPath} ({ex.Message})");
            }
        }

        return String.Join(Environment.NewLine, statuses);
    }

    private static string ResolveToolPath(string root, string suppliedPath, bool allowAbsolute)
    {
        if (String.IsNullOrWhiteSpace(suppliedPath))
            throw new ArgumentException("A file path is required.");

        if (!Path.IsPathRooted(suppliedPath))
            return ResolveWithinRoot(root, suppliedPath);

        if (!allowAbsolute)
            throw new InvalidOperationException("Absolute paths are not allowed for this operation.");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(suppliedPath);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("The path is outside the project root.");

        return ResolveWithinRoot(root, relative);
    }
}
