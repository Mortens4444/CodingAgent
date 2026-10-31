using CodingAgent.Commands;
using CodingAgent.Extensions;
using System.Text.Json;

namespace CodingAgent.Services;

public static class FileChangeTools
{
    public static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "write_file", "replace_in_file", "delete_file", "move_file"
    };

    // The project-relative paths a file-changing call touches.
    public static IReadOnlyList<string> PathsOf(string toolName, JsonElement args)
    {
        var paths = toolName.Equals("move_file", StringComparison.OrdinalIgnoreCase)
            ? new[] { args.GetFirstString(MoveFileCommand.SourceNames), args.GetFirstString(MoveFileCommand.DestinationNames) }
            : new[] { args.GetFirstString(Command.PathNames) };

        return paths.Where(path => !String.IsNullOrWhiteSpace(path)).Select(path => path!.Replace('\\', '/')).ToList();
    }

    // Null when the path is outside the root; the command itself reports that error.
    public static string? FullPath(string root, string relativePath)
    {
        try
        {
            if (Path.IsPathRooted(relativePath))
                return null;

            var fullRoot = Path.GetFullPath(root);
            var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            var relative = Path.GetRelativePath(fullRoot, fullPath);
            return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative)
                ? null
                : fullPath;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static string? ReadText(string? fullPath)
    {
        try
        {
            return fullPath is not null && File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

// Remembers each changed file's content from before its first change in this run, so the run can end with
// one diff of everything the agent changed.
public sealed class FileChangeTracker
{
    private readonly Dictionary<string, (string FullPath, string? Original)> originals = new(StringComparer.OrdinalIgnoreCase);

    public void Snapshot(string root, IEnumerable<string> relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var fullPath = FileChangeTools.FullPath(root, relativePath);
            if (fullPath is null)
                continue;

            var key = Path.GetRelativePath(Path.GetFullPath(root), fullPath).Replace('\\', '/');
            if (!originals.ContainsKey(key))
                originals[key] = (fullPath, FileChangeTools.ReadText(fullPath));
        }
    }

    public record FileChange(string Path, string Status, List<string> Diff);

    public List<FileChange> GetChanges()
    {
        var changes = new List<FileChange>();

        foreach (var (path, (fullPath, original)) in originals.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var current = FileChangeTools.ReadText(fullPath);
            if (current == original)
                continue;

            var status = original is null ? "created" : current is null ? "deleted" : "modified";
            changes.Add(new FileChange(path, status, LineDiff.Unified(original, current)));
        }

        return changes;
    }

    public void WriteReport()
    {
        var changes = GetChanges();
        if (changes.Count == 0)
            return;

        ConsoleOutput.WriteLine($"\n=== Changed files ({changes.Count}) ===", ConsoleOutput.Heading);
        foreach (var change in changes)
            ConsoleOutput.WriteLine($"  {change.Status,-9} {change.Path}", ConsoleOutput.Tool);

        foreach (var change in changes)
        {
            ConsoleOutput.WriteLine($"\n--- {change.Path} ({change.Status})", ConsoleOutput.Heading);
            WriteDiff(change.Diff);
        }
    }

    public static void WriteDiff(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var color = line.StartsWith("@@", StringComparison.Ordinal) ? ConsoleOutput.Heading :
                        line.StartsWith('+') ? ConsoleOutput.Success :
                        line.StartsWith('-') ? ConsoleOutput.Error :
                        ConsoleOutput.Tool;
            ConsoleOutput.WriteLine(line, color);
        }
    }
}

// CodingAgent:FileChanges:Approval decides whether file changes need the user's consent:
// Always (default, no question), Ask (the user confirms each change after seeing a preview), or Never (read-only run).
public sealed class FileChangeApproval(string mode, Func<string, bool>? askUser = null)
{
    private readonly Func<string, bool> askUser = askUser ?? UserApproval.Ask;

    public string Mode { get; } = mode;

    // Null when the change may run; otherwise the DENIED result for the model.
    public string? Check(string root, string toolName, JsonElement args)
    {
        if (Mode.Equals("Never", StringComparison.OrdinalIgnoreCase))
            return $"DENIED: File changes are disabled by configuration (CodingAgent:FileChanges:Approval=Never); {toolName} was not run. " +
                   "Do not try other ways to change files; describe the needed changes in the finish summary.";

        if (!Mode.Equals("Ask", StringComparison.OrdinalIgnoreCase))
            return null;

        var (title, preview) = Describe(root, toolName, args);
        ConsoleOutput.WriteLine($"\nThe agent wants to {title}", ConsoleOutput.Warning);
        FileChangeTracker.WriteDiff(preview);

        return askUser("Allow this change?")
            ? null
            : $"DENIED: The user did not allow this {toolName} call; nothing was changed. " +
              "Do not repeat the same change; try a different approach or explain the blocker in finish.";
    }

    public static (string Title, List<string> Preview) Describe(string root, string toolName, JsonElement args)
    {
        var paths = FileChangeTools.PathsOf(toolName, args);
        var path = paths.Count > 0 ? paths[0] : "(no path)";

        switch (toolName.ToLowerInvariant())
        {
            case "write_file":
                var current = FileChangeTools.ReadText(FileChangeTools.FullPath(root, path));
                WriteFileCommand.TryGetContent(args, out var content);
                return ($"{(current is null ? "create" : "overwrite")} {path}:", LineDiff.Unified(current, content, maxLines: 60));

            case "replace_in_file":
                var oldText = args.GetFirstString(ReplaceInFileCommand.OldTextNames);
                var newText = args.GetFirstString(ReplaceInFileCommand.NewTextNames);
                return ($"edit {path}:", LineDiff.Unified(oldText, newText, maxLines: 60));

            case "delete_file":
                return ($"delete {path}", []);

            case "move_file":
                return ($"move {path} -> {(paths.Count > 1 ? paths[1] : "(no destination)")}", []);

            default:
                return ($"run {toolName} on {path}", []);
        }
    }
}

public static class UserApproval
{
    public static bool Ask(string question)
    {
        ConsoleOutput.Write($"{question} [y/N] ", ConsoleOutput.Warning);
        var answer = Console.ReadLine()?.Trim();
        return answer is not null && (answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
