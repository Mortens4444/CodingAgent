using CodingAgent.Interfaces;
using CodingAgent.Services;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

public class SearchTextCommand : Command, IToolCommand
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".razor", ".cshtml", ".xaml", ".json", ".xml", ".csproj", ".props",
        ".targets", ".md", ".txt", ".config", ".yaml", ".yml", ".resx", ".sln", ".slnx",
        ".py", ".pyw", ".toml", ".ini", ".cfg", ".ipynb", ".sh", ".ps1", ".bat", ".sql"
    };

    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var query = GetString(args, "query");
        if (String.IsNullOrWhiteSpace(query))
            return "ERROR: query must not be empty.";
        if (query.Length < 2)
            return "ERROR: Use a search query of at least two characters.";

        var matches = new List<string>();
        var directories = new Stack<string>();
        directories.Push(codingLanguageService.Root);
        var scanned = 0;

        while (directories.Count > 0 && scanned < 5000 && matches.Count < 100)
        {
            var current = directories.Pop();
            try
            {
                foreach (var child in Directory.EnumerateDirectories(current))
                {
                    if (!ProjectITypedentifier.ExcludedDirectories.Contains(Path.GetFileName(child)))
                        directories.Push(child);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    if (++scanned > 5000 || matches.Count >= 100) break;
                    if (!TextExtensions.Contains(Path.GetExtension(file)) || new FileInfo(file).Length > 500_000) continue;

                    try
                    {
                        var lineNumber = 0;
                        var lines = await File.ReadLinesAsync(file).ToListAsync();
                        foreach (var line in lines)
                        {
                            lineNumber++;
                            if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                            {
                                matches.Add($"{ToRelativePath(codingLanguageService.Root, file)}:{lineNumber}: {line.Trim()}");
                                if (matches.Count >= 100) break;
                            }
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return $"Scanned approximately {scanned} files. Matches: {matches.Count}{(matches.Count >= 100 ? " (limit reached)" : String.Empty)}\n" +
               (matches.Count == 0 ? "No matches found." : String.Join(Environment.NewLine, matches));
    }
}
