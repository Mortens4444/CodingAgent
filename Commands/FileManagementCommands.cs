using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

public class DeleteFileCommand : Command, IToolCommand
{
    public Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var relativePath = GetFirstString(args, "path", "file_path");
        var file = ResolveWithinRoot(codingLanguageService.Root, relativePath);

        if (Directory.Exists(file))
            return Task.FromResult("ERROR: delete_file only deletes files, not directories.");
        if (!File.Exists(file))
            return Task.FromResult($"ERROR: File not found: {relativePath}");

        File.Delete(file);
        return Task.FromResult($"DELETE SUCCESS: {ToRelativePath(codingLanguageService.Root, file)} was deleted.");
    }
}

public class MoveFileCommand : Command, IToolCommand
{
    public Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var source = ResolveWithinRoot(codingLanguageService.Root, GetFirstString(args, "source", "from", "path", "old_path"));
        var destination = ResolveWithinRoot(codingLanguageService.Root, GetFirstString(args, "destination", "to", "new_path", "target"));

        if (!File.Exists(source))
            return Task.FromResult($"ERROR: Source file not found: {ToRelativePath(codingLanguageService.Root, source)}");
        if (File.Exists(destination) || Directory.Exists(destination))
            return Task.FromResult($"ERROR: Destination already exists: {ToRelativePath(codingLanguageService.Root, destination)}. Delete it first if it should be replaced.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination);
        return Task.FromResult($"MOVE SUCCESS: {ToRelativePath(codingLanguageService.Root, source)} -> {ToRelativePath(codingLanguageService.Root, destination)}");
    }
}

public class CheckSyntaxCommand : Command, IToolCommand
{
    public async Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        var result = await codingLanguageService.ValidateSyntaxAsync();
        return String.IsNullOrWhiteSpace(result)
            ? "No separate syntax check exists for this language; use build_project."
            : result;
    }
}
