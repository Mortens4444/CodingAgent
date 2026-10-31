using CodingAgent.Enums;

namespace CodingAgent.Services;

public static class LanguageClassifier
{
    // Checked before extensions. No name here has an extension that maps to a different language.
    private static readonly Dictionary<string, ProgrammingLanguage> FileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pyproject.toml"] = ProgrammingLanguage.Python,
        ["requirements.txt"] = ProgrammingLanguage.Python,
        ["setup.py"] = ProgrammingLanguage.Python,
        ["setup.cfg"] = ProgrammingLanguage.Python,
        ["Pipfile"] = ProgrammingLanguage.Python,
        ["CMakeLists.txt"] = ProgrammingLanguage.C,
        ["Makefile"] = ProgrammingLanguage.C,
        ["pom.xml"] = ProgrammingLanguage.Java,
        ["build.gradle"] = ProgrammingLanguage.Java,
        ["build.gradle.kts"] = ProgrammingLanguage.Java,
        ["package.json"] = ProgrammingLanguage.JavaScript,
        ["tsconfig.json"] = ProgrammingLanguage.TypeScript
    };

    private static readonly Dictionary<string, ProgrammingLanguage> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = ProgrammingLanguage.Python, [".pyw"] = ProgrammingLanguage.Python,
        [".c"] = ProgrammingLanguage.C, [".h"] = ProgrammingLanguage.C,
        [".cpp"] = ProgrammingLanguage.Cpp, [".cc"] = ProgrammingLanguage.Cpp, [".cxx"] = ProgrammingLanguage.Cpp, [".hpp"] = ProgrammingLanguage.Cpp,
        [".java"] = ProgrammingLanguage.Java,
        [".js"] = ProgrammingLanguage.JavaScript, [".mjs"] = ProgrammingLanguage.JavaScript,
        [".ts"] = ProgrammingLanguage.TypeScript, [".tsx"] = ProgrammingLanguage.TypeScript,
        [".html"] = ProgrammingLanguage.Html, [".htm"] = ProgrammingLanguage.Html,
        [".asm"] = ProgrammingLanguage.Assembly, [".s"] = ProgrammingLanguage.Assembly,
        [".pas"] = ProgrammingLanguage.Pascal, [".pp"] = ProgrammingLanguage.Pascal,
        [".lisp"] = ProgrammingLanguage.Lisp, [".lsp"] = ProgrammingLanguage.Lisp, [".cl"] = ProgrammingLanguage.Lisp
    };

    // Runs for every file of a directory scan, so it uses lookups and computes the relative path only for C# projects.
    public static (ProgrammingLanguage Language, string ProjectFile) IdentifyFromFile(string fullPath, string root)
    {
        var extension = Path.GetExtension(fullPath);

        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.CSharp, Path.GetRelativePath(root, fullPath));
        }

        if (FileNames.TryGetValue(Path.GetFileName(fullPath), out var language) ||
            Extensions.TryGetValue(extension, out language))
        {
            return (language, String.Empty);
        }

        return (ProgrammingLanguage.Unknown, String.Empty);
    }
}
