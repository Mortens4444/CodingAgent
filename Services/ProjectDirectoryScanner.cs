using CodingAgent.Enums;

namespace CodingAgent.Services;

public static class ProjectDirectoryScanner
{
    public static (ProgrammingLanguage Language, string ProjectFile) DetectProject(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        ProgrammingLanguage detectedLanguage = ProgrammingLanguage.Unknown;
        string projectFile = String.Empty;

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var (lang, proj) = LanguageClassifier.IdentifyFromFile(file, root);
                    if (lang != ProgrammingLanguage.Unknown)
                    {
                        if (!String.IsNullOrEmpty(proj) && String.IsNullOrEmpty(projectFile))
                        {
                            projectFile = proj;
                            detectedLanguage = lang;
                        }
                        else if (detectedLanguage == ProgrammingLanguage.Unknown)
                        {
                            detectedLanguage = lang;
                        }
                    }
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    if (!Constants.ExcludedDirectories.Contains(Path.GetFileName(directory)))
                        pending.Push(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return (detectedLanguage, projectFile);
    }
}