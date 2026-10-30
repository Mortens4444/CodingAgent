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
        var content = GetString(args, "content", GetString(args, "new_content"));

        if (String.IsNullOrWhiteSpace(relativePath))
            return "ERROR: Missing required argument 'path'.";

        var file = ResolveWithinRoot(codingLanguageService.Root, relativePath);
        var folder = Path.GetDirectoryName(file);

        Console.WriteLine($"[WriteFile] Target: {file}");

        if (folder is not null)
            Directory.CreateDirectory(folder);

        var exists = File.Exists(file);

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
}
