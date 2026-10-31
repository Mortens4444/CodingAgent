using CodingAgent.Interfaces;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

public class ReadFileCommand : Command, IToolCommand
{
    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var relativePath = GetString(args, "path", GetString(args, "file_path"));
        var file = ResolveWithinRoot(codingLanguageService.Root, relativePath);
        if (!File.Exists(file))
            return $"ERROR: File not found: {relativePath}";

        var info = new FileInfo(file);
        if (info.Length > 300_000)
            return "ERROR: File is larger than 300 KB. Search for the relevant section instead.";

        var fileContent = await File.ReadAllTextAsync(file);
        if (fileContent.Length <= Constants.ReadFilePageLength)
            return fileContent;

        return ReadPage(fileContent, GetStartLine(args));
    }

    // Large files are returned one page of whole lines at a time, so nothing is cut off silently.
    private static string ReadPage(string fileContent, int startLine)
    {
        var lines = fileContent.Split('\n');
        var first = Math.Clamp(startLine, 1, lines.Length);
        var page = new StringBuilder();
        var last = first - 1;

        while (last < lines.Length && (page.Length == 0 || page.Length + lines[last].Length + 1 <= Constants.ReadFilePageLength))
        {
            page.Append(lines[last]).Append('\n');
            last++;
        }

        var header = $"[Lines {first}-{last} of {lines.Length}.";
        header += last < lines.Length
            ? $" More follows: call read_file with \"start_line\":{last + 1}. write_file replaces the whole file, so read every page before rewriting it.]"
            : " End of file.]";

        return header + "\n" + page.ToString().TrimEnd('\n');
    }

    private static int GetStartLine(JsonElement args)
    {
        foreach (var name in new[] { "start_line", "line_number", "line", "offset" })
        {
            if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                    return number;
                if (value.ValueKind == JsonValueKind.String && Int32.TryParse(value.GetString(), out number))
                    return number;
            }
        }

        return 1;
    }
}
