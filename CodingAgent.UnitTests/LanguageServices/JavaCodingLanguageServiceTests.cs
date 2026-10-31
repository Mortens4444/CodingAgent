using Mocklis;
using CodingAgent.Services.LanguageServices;
using CodingAgent.UnitTests.Mocks;

namespace CodingAgent.UnitTests.LanguageServices;

[TestFixture]
public class JavaCodingLanguageServiceTests
{
    private TempWorkspace _workspace = null!;
    private RecordingProcessRunner _runner = null!;
    private JavaCodingLanguageService _service = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _runner = new RecordingProcessRunner();
        _service = new JavaCodingLanguageService(_workspace.Root, _runner.Mock);
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [Test]
    public async Task Maven_BuildAndTest()
    {
        _workspace.Write("pom.xml");

        await _service.BuildAsync();
        await _service.RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "mvn -B compile", "mvn -B test" }));
    }

    [Test]
    public async Task Maven_PrefersWrapper()
    {
        _workspace.Write("pom.xml");
        _workspace.Write(OperatingSystem.IsWindows() ? "mvnw.cmd" : "mvnw");

        await _service.BuildAsync();

        Assert.That(_runner.Commands[0], Does.StartWith("mvnw"));
    }

    [Test]
    public async Task Gradle_BuildAndTest()
    {
        _workspace.Write("build.gradle.kts");

        await _service.BuildAsync();
        await _service.RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "gradle classes", "gradle test" }));
    }

    [Test]
    public async Task Gradle_PrefersWrapper()
    {
        _workspace.Write("build.gradle");
        _workspace.Write(OperatingSystem.IsWindows() ? "gradlew.bat" : "gradlew");

        await _service.RunTestsAsync();

        Assert.That(_runner.Commands[0], Does.StartWith("gradlew"));
    }

    [Test]
    public async Task PlainSources_CompileWithJavacIntoBuildFolder()
    {
        _workspace.Write("src/App.java");
        _workspace.Write("Util.java");

        await _service.BuildAsync();
        await _service.ValidateSyntaxAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            "javac -d build/classes src/App.java Util.java",
            "javac -Xlint -proc:none -d build/classes src/App.java Util.java"
        }));
    }

    [Test]
    public async Task PlainSources_RunTests_WithoutTests_ExplainsHowToAddJUnit()
    {
        _workspace.Write("App.java");

        var result = await _service.RunTestsAsync();

        Assert.That(result, Does.StartWith("No runnable Java tests"));
        Assert.That(result, Does.Contain("lib/junit-platform-console-standalone"));
        Assert.That(_runner.Commands, Is.Empty);
    }

    [Test]
    public async Task PlainSources_WithJUnitJar_CompilesWithItAndRunsTheConsoleLauncher()
    {
        _workspace.Write("App.java");
        _workspace.Write("lib/junit-platform-console-standalone-1.10.0.jar");
        _workspace.Write("lib/junit-platform-console-standalone-1.11.4.jar");

        await _service.BuildAsync();
        await _service.RunTestsAsync();

        const string jar = "lib/junit-platform-console-standalone-1.11.4.jar";
        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            $"javac -d build/classes -cp {jar} App.java",
            $"javac -d build/classes -cp {jar} App.java",
            $"java -jar {jar} execute --class-path build/classes --scan-class-path --disable-banner"
        }));
    }

    [Test]
    public async Task PlainSources_RunsTestClassesWithMainMethods()
    {
        _workspace.Write("src/App.java", "class App {}");
        _workspace.Write("src/AppTest.java", "package demo;\npublic class AppTest { public static void main(String[] a) {} }");
        _workspace.Write("tests/OtherTests.java", "class OtherTests { static public void main(String[] a) {} }");
        _workspace.Write("tests/Helper.java", "class Helper {}");
        _workspace.Write("TestNoMain.java", "class TestNoMain {}");

        await _service.RunTestsAsync();

        Assert.That(_runner.Commands.Skip(1), Is.EqualTo(new[]
        {
            "java -cp build/classes demo.AppTest",
            "java -cp build/classes OtherTests"
        }));
    }

    [Test]
    public async Task PlainSources_RunsTheJavaNextToJavac()
    {
        _workspace.Write("ATest.java", "class ATest { public static void main(String[] a) {} }");
        var javac = _workspace.Write("jdk/bin/" + (OperatingSystem.IsWindows() ? "javac.exe" : "javac"));
        _workspace.Write("jdk/bin/" + (OperatingSystem.IsWindows() ? "java.exe" : "java"));
        var launched = new List<string>();
        _runner.Mock.FindExecutable.Func(name => name == "javac" ? javac : "/usr/bin/" + name);
        _runner.Mock.RunAsync.Func(call =>
        {
            launched.Add(call.executable);
            return Task.FromResult(RecordingProcessRunner.Success(""));
        });

        await _service.RunTestsAsync();

        Assert.That(launched[1], Is.EqualTo(Path.Combine(Path.GetDirectoryName(javac)!, OperatingSystem.IsWindows() ? "java.exe" : "java")));
    }

    [Test]
    public async Task PlainSources_FailingCompileOrTest_StopsEarly()
    {
        _workspace.Write("ATest.java", "class ATest { public static void main(String[] a) {} }");
        _workspace.Write("BTest.java", "class BTest { public static void main(String[] a) {} }");

        _runner.Result = (name, _) => name == "java" ? RecordingProcessRunner.Failure("assertion failed") : RecordingProcessRunner.Success("");
        var failedTest = await _service.RunTestsAsync();

        Assert.That(failedTest, Does.Contain("assertion failed"));
        Assert.That(_runner.Commands, Has.Count.EqualTo(2));

        _runner.Commands.Clear();
        _runner.Result = (_, _) => RecordingProcessRunner.Failure("syntax error");
        var failedCompile = await _service.RunTestsAsync();

        Assert.That(failedCompile, Does.Contain("syntax error"));
        Assert.That(_runner.Commands, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task NoSources_ReportsError()
    {
        var result = await _service.BuildAsync();

        Assert.That(result, Does.StartWith("ERROR: No Java source files"));
    }
}
