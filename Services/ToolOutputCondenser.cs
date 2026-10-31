using System.Text;
using System.Text.RegularExpressions;

namespace CodingAgent.Services;

// Build and test logs put the useful lines anywhere, and cutting them at a fixed length often keeps only
// restore/progress noise. Long output keeps its header, every error/failure line it can fit, and its tail.
public static partial class ToolOutputCondenser
{
    public static string Condense(string output, int maxLength)
    {
        if (output.Length <= maxLength)
            return output;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var header = lines.Take(2).ToList();
        var body = lines.Skip(2).ToList();

        var result = new StringBuilder();
        foreach (var line in header)
            result.Append(line).Append('\n');
        result.Append($"[Output shortened from {lines.Length} lines.]\n");

        var keyBudget = maxLength / 2;
        var keyLines = body.Where(line => KeyLineRegex().IsMatch(line)).Select(line => line.Trim()).Distinct().ToList();
        if (keyLines.Count > 0)
        {
            result.Append("[Error/failure lines:]\n");
            var used = 0;
            foreach (var line in keyLines)
            {
                if (used + line.Length + 1 > keyBudget) break;
                result.Append(line).Append('\n');
                used += line.Length + 1;
            }
        }

        var remaining = maxLength - result.Length - 20;
        var tail = new List<string>();
        for (var i = body.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var line = body[i].Length > remaining ? body[i][^remaining..] : body[i];
            tail.Insert(0, line);
            remaining -= line.Length + 1;
        }

        result.Append("[Last lines:]\n").Append(String.Join("\n", tail));
        return result.ToString();
    }

    [GeneratedRegex(@"\berror\b|\bfail(ed|ure|s)?\b|exception|assert|warning\s+[A-Z]+\d+|\bFAIL\b|traceback|undefined reference|cannot find|not found", RegexOptions.IgnoreCase)]
    private static partial Regex KeyLineRegex();
}
