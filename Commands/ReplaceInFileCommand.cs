using CodingAgent.Interfaces;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

// Replaces an exact snippet instead of the whole file, so a small model can change a large file without
// reproducing all of it. The snippet must be unique unless replace_all is set.
public class ReplaceInFileCommand : Command, IToolCommand
{
    private static readonly string[] OldTextNames = ["old_text", "old_str", "old_string", "search", "find"];
    private static readonly string[] NewTextNames = ["new_text", "new_str", "new_string", "replace", "replacement"];

    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var relativePath = GetFirstString(args, "path", "file_path");
        if (String.IsNullOrWhiteSpace(relativePath))
            return "ERROR: Missing required argument 'path'.";

        var oldText = GetFirstString(args, OldTextNames);
        if (oldText.Length == 0)
            return "ERROR: replace_in_file requires 'old_text': the exact existing text to replace (copy it from read_file).";
        if (!HasString(args, NewTextNames))
            return "ERROR: replace_in_file requires 'new_text' as a string (use \"\" to delete the old text).";
        var newText = GetFirstString(args, NewTextNames);

        var file = ResolveWithinRoot(codingLanguageService.Root, relativePath);
        if (!File.Exists(file))
            return $"ERROR: File not found: {relativePath}. Use write_file to create new files.";

        var content = await File.ReadAllTextAsync(file);

        // Models send \n even when the file uses \r\n.
        if (content.Contains("\r\n") && !oldText.Contains("\r\n"))
        {
            oldText = oldText.Replace("\n", "\r\n");
            newText = newText.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        var positions = FindAll(content, oldText);
        if (positions.Count == 0)
        {
            // Small models often get the indentation or trailing spaces wrong; accept a unique match that differs only there.
            if (TryReplaceIgnoringIndentation(content, oldText, newText, out var fuzzyUpdated, out var fuzzyLine, out var matches))
            {
                await File.WriteAllTextAsync(file, fuzzyUpdated, new UTF8Encoding(HasBom(file)));
                return $"EDIT SUCCESS: {ToRelativePath(codingLanguageService.Root, file)}: replaced 1 occurrence at line {fuzzyLine} " +
                       "(matched ignoring indentation; the file's indentation was kept).\n" +
                       Excerpt(fuzzyUpdated, fuzzyLine, newText.Count(c => c == '\n') + 1);
            }

            return matches > 1
                ? $"ERROR: old_text was not found exactly in {relativePath}, and it matches {matches} places when indentation is ignored. " +
                  "Read the file again and copy the exact lines, including more surrounding lines."
                : $"ERROR: old_text was not found in {relativePath}. It must match the file exactly, including indentation. " +
                  "Read the file again and copy the exact lines.";
        }

        if (positions.Count > 1 && !GetBool(args, "replace_all"))
            return $"ERROR: old_text occurs {positions.Count} times in {relativePath} (lines {String.Join(", ", positions.Select(p => LineOf(content, p)))}). " +
                   "Include more surrounding lines so it is unique, or set \"replace_all\":true.";

        var updated = content.Replace(oldText, newText, StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, updated, new UTF8Encoding(HasBom(file)));

        var line = LineOf(updated, positions[0]);
        return $"EDIT SUCCESS: {ToRelativePath(codingLanguageService.Root, file)}: replaced {positions.Count} occurrence(s), first at line {line}.\n" +
               Excerpt(updated, line, newText.Count(c => c == '\n') + 1);
    }

    private static List<int> FindAll(string content, string value)
    {
        var positions = new List<int>();
        for (var index = content.IndexOf(value, StringComparison.Ordinal); index >= 0; index = content.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            positions.Add(index);
        return positions;
    }

    // Compares whole lines with leading and trailing whitespace removed. On a unique match the lines are replaced
    // by new_text, re-indented by the difference between the file's indentation and the one the model used.
    private static bool TryReplaceIgnoringIndentation(string content, string oldText, string newText, out string updated, out int line, out int matches)
    {
        updated = content;
        line = 0;
        matches = 0;

        var newline = content.Contains("\r\n") ? "\r\n" : "\n";
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var oldLines = oldText.Replace("\r\n", "\n").Trim('\n').Split('\n');
        if (oldLines.All(String.IsNullOrWhiteSpace))
            return false;

        var trimmedLines = Array.ConvertAll(lines, l => l.Trim());
        var trimmedOld = Array.ConvertAll(oldLines, l => l.Trim());

        var start = -1;
        for (var i = 0; i + trimmedOld.Length <= trimmedLines.Length; i++)
        {
            if (!trimmedLines.AsSpan(i, trimmedOld.Length).SequenceEqual(trimmedOld))
                continue;

            matches++;
            start = i;
        }

        if (matches != 1)
            return false;

        var firstOld = Array.FindIndex(oldLines, l => !String.IsNullOrWhiteSpace(l));
        var modelIndent = Indentation(oldLines[firstOld]);
        var fileIndent = Indentation(lines[start + firstOld]);

        var newLines = newText.Length == 0
            ? Enumerable.Empty<string>()
            : newText.Replace("\r\n", "\n").Trim('\n').Split('\n').Select(newLine => Reindent(newLine, modelIndent, fileIndent));

        updated = String.Join(newline, lines.Take(start).Concat(newLines).Concat(lines.Skip(start + oldLines.Length)));
        line = start + 1;
        return true;
    }

    private static string Indentation(string text) => text[..(text.Length - text.TrimStart().Length)];

    private static string Reindent(string text, string modelIndent, string fileIndent)
    {
        if (String.IsNullOrWhiteSpace(text))
            return text.Trim();
        if (text.StartsWith(modelIndent, StringComparison.Ordinal))
            return fileIndent + text[modelIndent.Length..];
        return fileIndent + text.TrimStart();
    }

    private static int LineOf(string content, int index) => content.AsSpan(0, index).Count('\n') + 1;

    // Shows the edited lines with a little context so the model can check the result without another read.
    private static string Excerpt(string content, int firstLine, int lineCount)
    {
        var lines = content.Split('\n');
        var from = Math.Max(1, firstLine - 2);
        var to = Math.Min(lines.Length, firstLine + Math.Min(lineCount, 30) + 1);
        return String.Join("\n", Enumerable.Range(from, to - from + 1).Select(n => $"{n,5}: {lines[n - 1].TrimEnd('\r')}"));
    }

    private static bool HasBom(string file)
    {
        using var stream = File.OpenRead(file);
        Span<byte> bom = stackalloc byte[3];
        return stream.Read(bom) == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF;
    }
}
