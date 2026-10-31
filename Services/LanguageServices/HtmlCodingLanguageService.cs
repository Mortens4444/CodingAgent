using CodingAgent.Interfaces;
using System.Text.RegularExpressions;

namespace CodingAgent.Services.LanguageServices;

// Uses htmlhint when the project has it; otherwise a built-in check for unclosed and mismatched tags.
public partial class HtmlCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr", "!doctype"
    };

    // Elements whose end tag HTML allows to be left out.
    private static readonly HashSet<string> OptionalEndTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body", "p", "li", "dt", "dd", "tr", "td", "th", "thead", "tbody", "tfoot",
        "option", "optgroup", "colgroup", "caption", "rt", "rp"
    };

    public async Task<string> BuildAsync() =>
        "HTML is a markup language and does not require building; validated it instead.\n" + await ValidateSyntaxAsync();

    public Task<string> RunTestsAsync() =>
        NodeProject.HasScript(Root, "test")
            ? RunProcessAsync("npm", ["test"])
            : Task.FromResult("No automated tests configured for HTML (add a \"test\" script to package.json).");

    public async Task<string> ValidateSyntaxAsync()
    {
        if (HasFile(".htmlhintrc") || NodeProject.HasLocalBinary(Root, "htmlhint"))
            return await RunProcessAsync("npx", ["htmlhint", "."]);

        var files = FindSourceFiles(".html", ".htm");
        if (files.Count == 0)
            return NoSourcesMessage("HTML");

        var problems = new List<string>();
        foreach (var file in files)
            problems.AddRange(CheckTags(File.ReadAllText(Path.Combine(Root, file))).Select(problem => $"{file}: {problem}"));

        return problems.Count == 0
            ? $"Command: built-in HTML tag check\nExit code: 0\nChecked {files.Count} HTML files; no tag problems found."
            : $"Command: built-in HTML tag check\nExit code: 1\n" + String.Join("\n", problems);
    }

    public static List<string> CheckTags(string html)
    {
        var problems = new List<string>();
        var open = new List<(string Name, int Line)>();
        var text = RawTextRegex().Replace(CommentRegex().Replace(html, m => new string('\n', m.Value.Count(c => c == '\n'))),
            m => m.Groups["open"].Value + new string('\n', m.Groups["body"].Value.Count(c => c == '\n')) + m.Groups["close"].Value);

        foreach (Match tag in TagRegex().Matches(text))
        {
            var name = tag.Groups["name"].Value.ToLowerInvariant();
            var line = text.AsSpan(0, tag.Index).Count('\n') + 1;

            if (tag.Groups["closing"].Success)
            {
                var index = open.FindLastIndex(entry => entry.Name == name);
                if (index < 0)
                {
                    problems.Add($"line {line}: closing tag </{name}> has no matching opening tag.");
                    continue;
                }

                foreach (var unclosed in open.Skip(index + 1).Where(entry => !OptionalEndTags.Contains(entry.Name)))
                    problems.Add($"line {unclosed.Line}: <{unclosed.Name}> is not closed before </{name}> on line {line}.");
                open.RemoveRange(index, open.Count - index);
            }
            else if (!VoidElements.Contains(name) && !tag.Groups["selfclosing"].Success)
            {
                open.Add((name, line));
            }
        }

        problems.AddRange(open.Where(entry => !OptionalEndTags.Contains(entry.Name))
            .Select(entry => $"line {entry.Line}: <{entry.Name}> is never closed."));
        return problems;
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"(?<open><(?<tag>script|style|textarea)\b[^>]*>)(?<body>.*?)(?<close></\k<tag>\s*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RawTextRegex();

    [GeneratedRegex(@"<(?<closing>/)?(?<name>!doctype|[a-zA-Z][a-zA-Z0-9-]*)\b(?:[^>""']|""[^""]*""|'[^']*')*?(?<selfclosing>/)?>")]
    private static partial Regex TagRegex();
}
