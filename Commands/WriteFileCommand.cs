using CodingAgent.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Commands;

internal class WriteFileCommand(IConfiguration configuration) : Command, IToolCommand
{
    private readonly bool backup = configuration.GetValue("CodingAgent:WriteFile:Backup", false);

    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var relativePath = GetString(args, "path", GetString(args, "file_path"));
        var content = GetString(args, "content", GetString(args, "new_content"));
        var file = ResolveWithinRoot(codingLanguageService.Root, relativePath);
        var folder = Path.GetDirectoryName(file)!;
        Directory.CreateDirectory(folder);

        var exists = File.Exists(file);
        var oldText = exists ? await File.ReadAllTextAsync(file) : String.Empty;

        if (exists && backup)
        {
            var backupRoot = Path.Combine(codingLanguageService.Root, ".codingagent-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            var backupPath = Path.Combine(backupRoot, ToRelativePath(codingLanguageService.Root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            await File.WriteAllTextAsync(backupPath, oldText, new UTF8Encoding(false));
        }

        await File.WriteAllTextAsync(file, content, new UTF8Encoding(false));
        return $"WRITE SUCCESS: {ToRelativePath(codingLanguageService.Root, file)} was {(exists ? "updated" : "created")}." +
               (exists ? " The old version saved into .codingagent-backups folder." : String.Empty);
    }
}
