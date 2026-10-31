using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

// Prefers the project's Maven/Gradle wrapper, then the installed tool, then plain javac over every .java file.
public class JavaCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private const string ClassesDirectory = "build/classes";

    public Task<string> BuildAsync()
    {
        if (HasFile("pom.xml"))
            return RunProcessAsync(Maven, ["-B", "compile"]);
        if (IsGradleProject)
            return RunProcessAsync(Gradle, ["classes"]);

        return CompileAsync(["-d", ClassesDirectory]);
    }

    public Task<string> RunTestsAsync()
    {
        if (HasFile("pom.xml"))
            return RunProcessAsync(Maven, ["-B", "test"]);
        if (IsGradleProject)
            return RunProcessAsync(Gradle, ["test"]);

        return Task.FromResult("No build tool (Maven/Gradle) configured for running automated Java tests. Add a pom.xml or build.gradle with JUnit to enable run_tests.");
    }

    public Task<string> ValidateSyntaxAsync() => CompileAsync(["-Xlint", "-proc:none", "-d", ClassesDirectory]);

    private bool IsGradleProject => HasFile("build.gradle", "build.gradle.kts");

    private string Maven => Wrapper("mvnw") ?? "mvn";

    private string Gradle => Wrapper("gradlew") ?? "gradle";

    private string? Wrapper(string name)
    {
        var file = IsWindows ? name + (name == "gradlew" ? ".bat" : ".cmd") : name;
        return HasFile(file) ? Path.Combine(Root, file) : null;
    }

    private async Task<string> CompileAsync(string[] options)
    {
        var sources = FindSourceFiles(".java");
        return sources.Count == 0
            ? NoSourcesMessage("Java")
            : await RunProcessAsync("javac", [.. options, .. sources]);
    }
}
