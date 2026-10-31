using CodingAgent.Interfaces;
using System.Text.RegularExpressions;

namespace CodingAgent.Services.LanguageServices;

// Prefers the project's Maven/Gradle wrapper, then the installed tool, then plain javac over every .java file.
// Plain projects run tests with the JUnit console launcher when its standalone jar is in the project (root, lib/
// or libs/); without it, test classes that have a main method are run one by one and must exit non-zero on failure.
public partial class JavaCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private const string ClassesDirectory = "build/classes";

    public const string JUnitJarPrefix = "junit-platform-console-standalone";

    private const string JUnitDownload =
        "https://repo1.maven.org/maven2/org/junit/platform/junit-platform-console-standalone/1.11.4/junit-platform-console-standalone-1.11.4.jar";

    public Task<string> BuildAsync()
    {
        if (HasFile("pom.xml"))
            return RunProcessAsync(Maven, ["-B", "compile"]);
        if (IsGradleProject)
            return RunProcessAsync(Gradle, ["classes"]);

        return CompileAsync(["-d", ClassesDirectory]);
    }

    public async Task<string> RunTestsAsync()
    {
        if (HasFile("pom.xml"))
            return await RunProcessAsync(Maven, ["-B", "test"]);
        if (IsGradleProject)
            return await RunProcessAsync(Gradle, ["test"]);

        var junit = FindJUnitJar();
        var mainTests = junit is null ? FindMainTestClasses() : [];

        if (junit is null && mainTests.Count == 0)
            return "No runnable Java tests. Without Maven/Gradle, either put the JUnit console launcher into lib/ " +
                   $"(run_command {{\"command\":\"curl\",\"args\":[\"-L\",\"-o\",\"lib/{JUnitJarPrefix}-1.11.4.jar\",\"{JUnitDownload}\"]}}) " +
                   "and write JUnit 5 tests, or add test classes under test/ or tests/ with a main method that exits " +
                   "with System.exit(1) on failure. A pom.xml or build.gradle with JUnit also works.";

        var compile = await CompileAsync(["-d", ClassesDirectory]);
        if (!Succeeded(compile))
            return compile;

        if (junit is not null)
            return compile + "\n\n" + await RunProcessAsync(JavaLauncher,
                ["-jar", junit, "execute", "--class-path", ClassesDirectory, "--scan-class-path", "--disable-banner"]);

        var results = new List<string> { compile };
        foreach (var testClass in mainTests)
        {
            var result = await RunProcessAsync(JavaLauncher, ["-cp", ClassesDirectory, testClass]);
            results.Add(result);
            if (!Succeeded(result))
                break;
        }

        return String.Join("\n\n", results);
    }

    public Task<string> ValidateSyntaxAsync() => CompileAsync(["-Xlint", "-proc:none", "-d", ClassesDirectory]);

    private bool IsGradleProject => HasFile("build.gradle", "build.gradle.kts");

    // The java next to the javac that compiled the classes; an older JRE earlier on PATH cannot load them.
    private string JavaLauncher
    {
        get
        {
            var javac = ProcessRunner.FindExecutable("javac");
            var java = javac is null ? null : Path.Combine(Path.GetDirectoryName(javac)!, ExecutableName("java"));
            return java is not null && File.Exists(java) ? java : "java";
        }
    }

    private string Maven => Wrapper("mvnw") ?? "mvn";

    private string Gradle => Wrapper("gradlew") ?? "gradle";

    private string? Wrapper(string name)
    {
        var file = IsWindows ? name + (name == "gradlew" ? ".bat" : ".cmd") : name;
        return HasFile(file) ? Path.Combine(Root, file) : null;
    }

    // Relative path of the newest standalone JUnit launcher in the root, lib/ or libs/.
    private string? FindJUnitJar() =>
        new[] { ".", "lib", "libs" }
            .Select(folder => Path.Combine(Root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, JUnitJarPrefix + "*.jar"))
            .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/'))
            .OrderByDescending(file => file, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    // Fully qualified names of test classes that can run on their own.
    private List<string> FindMainTestClasses()
    {
        var classes = new List<string>();
        foreach (var file in FindSourceFiles(".java").Where(IsJavaTestFile))
        {
            string source;
            try { source = File.ReadAllText(Path.Combine(Root, file)); }
            catch (IOException) { continue; }

            if (!MainMethodRegex().IsMatch(source))
                continue;

            var package = PackageRegex().Match(source);
            var name = Path.GetFileNameWithoutExtension(file);
            classes.Add(package.Success ? package.Groups[1].Value + "." + name : name);
        }

        return classes;
    }

    // Java names tests FooTest/TestFoo/FooTests as well as the shared test_* and tests/ conventions.
    private static bool IsJavaTestFile(string relativePath)
    {
        var name = Path.GetFileNameWithoutExtension(relativePath);
        return IsTestFile(relativePath) ||
               name.EndsWith("Test", StringComparison.Ordinal) ||
               name.EndsWith("Tests", StringComparison.Ordinal) ||
               (name.StartsWith("Test", StringComparison.Ordinal) && name.Length > 4 && Char.IsUpper(name[4]));
    }

    private async Task<string> CompileAsync(string[] options)
    {
        var sources = FindSourceFiles(".java");
        if (sources.Count == 0)
            return NoSourcesMessage("Java");

        // Tests that use JUnit need its jar to compile.
        var junit = FindJUnitJar();
        string[] classPath = junit is null ? [] : ["-cp", junit];
        return await RunProcessAsync("javac", [.. options, .. classPath, .. sources]);
    }

    [GeneratedRegex(@"\bstatic\s+(?:public\s+)?void\s+main\s*\(")]
    private static partial Regex MainMethodRegex();

    [GeneratedRegex(@"^\s*package\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex PackageRegex();
}
