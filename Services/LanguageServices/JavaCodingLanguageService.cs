using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class JavaCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public Task<string> BuildAsync()
    {
        if (File.Exists(Path.Combine(Root, "pom.xml")))
            return RunProcessAsync("mvn", Root, ["compile"], "mvn compile");
        if (File.Exists(Path.Combine(Root, "build.gradle")) || File.Exists(Path.Combine(Root, "build.gradle.kts")))
            return RunProcessAsync("gradle", Root, ["classes"], "gradle classes");

        return RunProcessAsync("javac", Root, ["*.java"], "javac *.java");
    }

    public Task<string> RunTestsAsync()
    {
        if (File.Exists(Path.Combine(Root, "pom.xml")))
            return RunProcessAsync("mvn", Root, ["test"], "mvn test");
        if (File.Exists(Path.Combine(Root, "build.gradle")) || File.Exists(Path.Combine(Root, "build.gradle.kts")))
            return RunProcessAsync("gradle", Root, ["test"], "gradle test");

        return Task.FromResult("No build tool (Maven/Gradle) configured for running automated Java tests.");
    }

    public Task<string> ValidateSyntaxAsync() =>
        RunProcessAsync("javac", Root, ["-Xlint", "-proc:none", "*.java"], "javac -Xlint *.java");
}