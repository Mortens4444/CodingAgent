using CodingAgent.Interfaces;
using CodingAgent.Services;
using System.IO.Enumeration;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

public class SearchTextCommand : Command, IToolCommand
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common project and text files
        ".cs", ".razor", ".cshtml", ".xaml", ".json", ".xml", ".csproj", ".props",
        ".targets", ".md", ".txt", ".config", ".yaml", ".yml", ".resx", ".sln", ".slnx",
        ".py", ".pyw", ".toml", ".ini", ".cfg", ".ipynb", ".sh", ".ps1", ".bat", ".sql",
        // Other supported languages
        ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp", ".java", ".js", ".mjs",
        ".ts", ".tsx", ".html", ".htm", ".asm", ".s", ".pas", ".pp", ".lisp", ".lsp", ".cl"
    };

    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var query = GetString(args, "query", GetString(args, "pattern", GetString(args, "text")));
        if (String.IsNullOrWhiteSpace(query))
            return "ERROR: query must not be empty.";
        if (query.Length < 2)
            return "ERROR: Use a search query of at least two characters.";

        // A wildcard query such as "*Tests.cs" (search_files style) matches file names instead of contents.
        var isFileNamePattern = query.IndexOfAny(['*', '?']) >= 0;

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
                    if (!Constants.ExcludedDirectories.Contains(Path.GetFileName(child)))
                        directories.Push(child);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    if (++scanned > 5000 || matches.Count >= 100) break;

                    if (isFileNamePattern)
                    {
                        if (FileSystemName.MatchesSimpleExpression(query, Path.GetFileName(file)))
                            matches.Add(ToRelativePath(codingLanguageService.Root, file));
                        continue;
                    }

                    if (!TextExtensions.Contains(Path.GetExtension(file)) || new FileInfo(file).Length > 500_000) continue;

                    try
                    {
                        var lineNumber = 0;
                        await foreach (var line in File.ReadLinesAsync(file))
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