using CodingAgent.Dto;
using CodingAgent.Enums;

namespace CodingAgent.Services;

public class ProjectIdentifier
{
    public ResolvedProject ResolveProject(string target)
    {
        var fullTarget = Path.GetFullPath(target);
        if (File.Exists(fullTarget))
        {
            var parent = Path.GetDirectoryName(fullTarget)!;
            var (lang, projFile) = LanguageClassifier.IdentifyFromFile(fullTarget, parent);

            if (lang != ProgrammingLanguage.Unknown)
            {
                return new ResolvedProject(parent, projFile, lang);
            }

            throw new InvalidOperationException(
                "You can specify a valid project description, solution, or source code file. Otherwise, specify the project folder.");
        }

        if (!Directory.Exists(fullTarget))
            throw new DirectoryNotFoundException($"The specified route cannot be found: {fullTarget}");

        var solutions = Directory.GetFiles(fullTarget, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(fullTarget, "*.slnx", SearchOption.TopDirectoryOnly))
            .ToArray();

        if (solutions.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple solution files found. Please specify the path of the .sln/.slnx file you want to use: " +
                String.Join(", ", solutions.Select(Path.GetFileName)));
        }

        if (solutions.Length == 1)
            return new ResolvedProject(fullTarget, Path.GetFileName(solutions[0]), ProgrammingLanguage.CSharp);

        var (detectedLang, projectFile) = ProjectDirectoryScanner.DetectProject(fullTarget);
        return new ResolvedProject(fullTarget, projectFile, detectedLang);
    }
}