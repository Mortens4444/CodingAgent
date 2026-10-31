using CodingAgent.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

public class WriteFileCommand(IConfiguration configuration) : Command, IToolCommand
{
    private readonly bool backup = configuration.GetValue("CodingAgent:WriteFile:Backup", false);


    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var relativePath = GetString(args, "path", GetString(args, "file_path"));

        if (String.IsNullOrWhiteSpace(relativePath))
            return "ERROR: Missing required argument 'path'.";

        if (!TryGetContent(args, out var content))
            return "ERROR: write_file requires 'content' as a string holding the complete new file content. " +
                   "Nothing was written. For a partial change use replace_in_file with old_text/new_text.";

        var file = ResolveWithinRoot(codingLanguageService.Root, relativePath);
        var folder = Path.GetDirectoryName(file);

        Console.WriteLine($"[WriteFile] Target: {file}");

        if (folder is not null)
            Directory.CreateDirectory(folder);

        var exists = File.Exists(file);

        if (exists && !GetBool(args, "allow_shrink"))
        {
            var oldLength = new FileInfo(file).Length;
            if (oldLength > Constants.ReadFilePageLength && Encoding.UTF8.GetByteCount(content) < oldLength / 2)
                return $"ERROR: The new content is less than half the size of the existing {oldLength}-byte file, so it was not written. " +
                       "Read every page of the file and send its complete content. " +
                       "If removing most of the file is intended, repeat the call with \"allow_shrink\":true.";
        }

        if (exists && backup)
        {
            var backupRoot = Path.Combine(
                codingLanguageService.Root,
                ".codingagent-backups",
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));

            var backupPath = Path.Combine(
                backupRoot,
                ToRelativePath(codingLanguageService.Root, file));

            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

            var oldText = await File.ReadAllTextAsync(file);
            await File.WriteAllTextAsync(backupPath, oldText, new UTF8Encoding(false));
        }

        await File.WriteAllTextAsync(file, content, new UTF8Encoding(false));

        if (!File.Exists(file))
            return $"ERROR: File was not created: {file}";

        Console.WriteLine($"[WriteFile] Created/updated: {file}");

        return $"WRITE SUCCESS: {ToRelativePath(codingLanguageService.Root, file)} was {(exists ? "updated" : "created")}." +
               (exists && backup ? " The old version was saved into .codingagent-backups." : String.Empty);
    }

    private static bool TryGetContent(JsonElement args, out string content)
    {
        foreach (var name in new[] { "content", "new_content" })
        {
            if (args.ValueKind == JsonValueKind.Object &&
                args.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                content = value.GetString() ?? String.Empty;
                return true;
            }
        }

        content = String.Empty;
        return false;
    }
}
