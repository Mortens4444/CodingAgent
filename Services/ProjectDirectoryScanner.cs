using CodingAgent.Enums;

namespace CodingAgent.Services;

public static class ProjectDirectoryScanner
{
    // Build descriptors that name the language outweigh single source files.
    private static readonly HashSet<string> ProjectMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "tsconfig.json", "pom.xml", "build.gradle", "build.gradle.kts",
        "pyproject.toml", "requirements.txt", "setup.py", "setup.cfg", "Pipfile"
    };

    // Shared by several languages (C/C++ builds, headers, JavaScript/TypeScript packages), so they only decide when nothing else does.
    private static readonly HashSet<string> NeutralFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "CMakeLists.txt", "Makefile", "makefile", "package.json"
    };

    public static (ProgrammingLanguage Language, string ProjectFile) DetectProject(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        var scores = new Dictionary<ProgrammingLanguage, int>();
        var fallbackLanguage = ProgrammingLanguage.Unknown;

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var (lang, proj) = LanguageClassifier.IdentifyFromFile(file, root);
                    if (lang == ProgrammingLanguage.Unknown)
                        continue;

                    // The first project file decides the result, so the rest of the tree need not be scanned.
                    if (!String.IsNullOrEmpty(proj))
                        return (lang, proj);

                    var weight = GetWeight(file);
                    if (weight == 0)
                    {
                        if (fallbackLanguage == ProgrammingLanguage.Unknown)
                            fallbackLanguage = lang;
                        continue;
                    }

                    scores[lang] = scores.GetValueOrDefault(lang) + weight;
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    if (!Constants.ExcludedDirectories.Contains(Path.GetFileName(directory)))
                        pending.Push(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        // Dictionary keeps insertion order here, so ties go to the language seen first.
        var detected = scores.Count == 0
            ? fallbackLanguage
            : scores.MaxBy(score => score.Value).Key;

        return (detected, String.Empty);
    }

    private static int GetWeight(string file)
    {
        var fileName = Path.GetFileName(file);
        if (NeutralFiles.Contains(fileName) || Path.GetExtension(file).Equals(".h", StringComparison.OrdinalIgnoreCase))
            return 0;

        return ProjectMarkers.Contains(fileName) ? 3 : 1;
    }
}
