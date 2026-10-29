using CodingAgent.Extensions;
using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

internal class ReadFileCommand : Command, IToolCommand
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
        return fileContent.Limit(300000);
    }
}
