using CodingAgent.Enums;

namespace CodingAgent.Services;

public static class LanguageClassifier
{
    public static (ProgrammingLanguage Language, string ProjectFile) IdentifyFromFile(string fullPath, string root)
    {
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);
        var relativePath = Path.GetRelativePath(root, fullPath);

        // CSharp
        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.CSharp, relativePath);
        }

        // Python
        if (fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("setup.cfg", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Pipfile", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".pyw", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Python, String.Empty);
        }

        // C
        if (fileName.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Makefile", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("makefile", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".c", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".h", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.C, String.Empty);
        }

        // C++
        if (extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cc", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cxx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".hpp", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Cpp, String.Empty);
        }

        // Java
        if (fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".java", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Java, String.Empty);
        }

        // JavaScript
        if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.JavaScript, String.Empty);
        }

        // TypeScript
        if (fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.TypeScript, String.Empty);
        }

        // HTML
        if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Html, String.Empty);
        }

        // Assembly
        if (extension.Equals(".asm", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".s", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Assembly, String.Empty);
        }

        // Pascal
        if (extension.Equals(".pas", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".pp", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Pascal, String.Empty);
        }

        // Lisp
        if (extension.Equals(".lisp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".lsp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cl", StringComparison.OrdinalIgnoreCase))
        {
            return (ProgrammingLanguage.Lisp, String.Empty);
        }

        return (ProgrammingLanguage.Unknown, String.Empty);
    }
}