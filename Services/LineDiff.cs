namespace CodingAgent.Services;

// A small line-based unified diff for the console: approval previews and the change summary at the end of a run.
// Lines start with ' ' (context), '-' (removed), '+' (added) or "@@" (hunk header).
public static class LineDiff
{
    // The LCS table needs old * new cells; larger files are only summarized.
    private const long MaxCells = 4_000_000;

    public static List<string> Unified(string? oldText, string? newText, int context = 2, int maxLines = 200)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        if ((long)oldLines.Length * newLines.Length > MaxCells)
            return [$"@@ too large to compare line by line: {oldLines.Length} -> {newLines.Length} lines @@"];

        var edits = Compare(oldLines, newLines);
        var output = new List<string>();
        var index = 0;

        while (index < edits.Count)
        {
            var start = edits.FindIndex(index, edit => edit.Kind != ' ');
            if (start < 0)
                break;

            // A hunk grows while the next change is within two context windows of the previous one.
            var end = start;
            for (var next = start; next < edits.Count; next++)
            {
                if (edits[next].Kind != ' ') end = next;
                else if (next - end > context * 2) break;
            }

            var first = Math.Max(index, start - context);
            var last = Math.Min(edits.Count - 1, end + context);
            output.Add($"@@ -{edits[first].OldLine} +{edits[first].NewLine} @@");
            for (var i = first; i <= last; i++)
                output.Add(edits[i].Kind + edits[i].Text);

            index = last + 1;
        }

        if (output.Count > maxLines)
        {
            var hidden = output.Count - maxLines;
            output.RemoveRange(maxLines, hidden);
            output.Add($"@@ {hidden} more diff lines not shown @@");
        }

        return output;
    }

    public static string[] SplitLines(string? text)
    {
        if (String.IsNullOrEmpty(text))
            return [];

        var lines = text.Replace("\r\n", "\n").Split('\n');
        return text.EndsWith('\n') ? lines[..^1] : lines;
    }

    private readonly record struct Edit(char Kind, string Text, int OldLine, int NewLine);

    private static List<Edit> Compare(string[] oldLines, string[] newLines)
    {
        // lengths[i, j] is the longest common subsequence of oldLines[i..] and newLines[j..].
        var lengths = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var i = oldLines.Length - 1; i >= 0; i--)
            for (var j = newLines.Length - 1; j >= 0; j--)
                lengths[i, j] = oldLines[i] == newLines[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

        var edits = new List<Edit>();
        int o = 0, n = 0;
        while (o < oldLines.Length || n < newLines.Length)
        {
            if (o < oldLines.Length && n < newLines.Length && oldLines[o] == newLines[n])
            {
                edits.Add(new(' ', oldLines[o], o + 1, n + 1));
                o++; n++;
            }
            // Removals come before additions, as in git diff.
            else if (o < oldLines.Length && (n == newLines.Length || lengths[o + 1, n] >= lengths[o, n + 1]))
            {
                edits.Add(new('-', oldLines[o], o + 1, n + 1));
                o++;
            }
            else
            {
                edits.Add(new('+', newLines[n], o + 1, n + 1));
                n++;
            }
        }

        return edits;
    }
}
