using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodingAgent.Services;

// What the agent has done so far. It survives history trimming, so a small context window does not make
// the model forget which files it already changed or whether the last build passed.
public sealed partial class AgentProgress
{
    public const string NotePrefix = "HOST PROGRESS NOTE";

    private static readonly HashSet<string> ChangingTools = FileChangeTools.Names;

    private static readonly HashSet<string> ValidatingTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "build_project", "check_syntax", "run_tests"
    };

    private readonly List<string> changedFiles = [];
    private readonly Dictionary<string, string> validations = new(StringComparer.OrdinalIgnoreCase);

    public bool HasUnvalidatedChanges { get; private set; }

    // Grows with every successful file change, so callers can tell whether anything changed since a point in time.
    public int ChangeCount { get; private set; }

    // True when the most recent build_project/check_syntax/run_tests did not pass.
    public bool LastValidationFailed { get; private set; }

    public bool IsEmpty => changedFiles.Count == 0 && validations.Count == 0;

    public void Record(string toolName, JsonElement arguments, string result)
    {
        if (ChangingTools.Contains(toolName) && result.Contains(" SUCCESS", StringComparison.Ordinal))
        {
            var path = GetPath(arguments);
            if (!String.IsNullOrWhiteSpace(path))
            {
                changedFiles.Remove(path);
                changedFiles.Add(path);
            }

            HasUnvalidatedChanges = true;
            ChangeCount++;
        }
        else if (ValidatingTools.Contains(toolName))
        {
            var passed = Passed(result);
            validations[toolName] = passed
                ? "passed"
                : "failed or did not run (see its last result)";
            LastValidationFailed = !passed;
            HasUnvalidatedChanges = false;
        }
    }

    public string ToNote()
    {
        var files = changedFiles.Count == 0 ? "none" : String.Join(", ", changedFiles.TakeLast(30));
        var checks = validations.Count == 0
            ? "none yet"
            : String.Join("; ", validations.Select(entry => $"{entry.Key} {entry.Value}"));

        return $"{NotePrefix} (older messages were removed to fit the context window):\n" +
               $"Files changed so far: {files}\n" +
               $"Last validation: {checks}" +
               (HasUnvalidatedChanges ? "\nFiles changed since the last build/test." : String.Empty);
    }

    // Several commands can run per step (C test build + run, one node --check per file); all must succeed.
    public static bool Passed(string result) =>
        ExitZeroRegex().IsMatch(result) && !ExitNonZeroRegex().IsMatch(result);

    [GeneratedRegex(@"Exit code: 0\b")]
    private static partial Regex ExitZeroRegex();

    [GeneratedRegex(@"Exit code: (-|[1-9])")]
    private static partial Regex ExitNonZeroRegex();

    private static string GetPath(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
            return String.Empty;

        foreach (var name in new[] { "destination", "to", "new_path", "path", "file_path" })
        {
            if (arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString()!.Replace('\\', '/');
        }

        return String.Empty;
    }
}
