using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

public class ListFilesCommand : Command, IToolCommand
{
    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var relativePath = GetString(args, "path", GetString(args, "directory", "."));
        if (Path.IsPathRooted(relativePath))
            relativePath = Path.GetRelativePath(codingLanguageService.Root, Path.GetFullPath(relativePath));

        var directory = ResolveWithinRoot(codingLanguageService.Root, relativePath);
        if (!Directory.Exists(directory))
            return $"ERROR: A könyvtár nem található: {relativePath}";

        var entries = new List<string>();
        try
        {
            entries.AddRange(Directory.EnumerateDirectories(directory)
                .Where(child => !Constants.ExcludedDirectories.Contains(Path.GetFileName(child)))
                .Select(child => "[DIR]  " + ToRelativePath(codingLanguageService.Root, child) + "/"));
            entries.AddRange(Directory.EnumerateFiles(directory)
                .Select(file => "[FILE] " + ToRelativePath(codingLanguageService.Root, file)));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return $"ERROR: Cannot list directory: {ex.Message}";
        }

        entries = [.. entries.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        var total = entries.Count;
        var truncated = entries.Take(160).ToList();

        return $"Directory: {ToRelativePath(codingLanguageService.Root, directory)}; entries: {total}" +
               (total > truncated.Count ? $" (showing first {truncated.Count}; inspect subdirectories with list_files)" : String.Empty) +
               "\n" + String.Join(Environment.NewLine, truncated);
    }
}