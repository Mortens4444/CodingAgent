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
    public async Task PlainSources_RunTests_ExplainsBuildToolIsNeeded()
    {
        _workspace.Write("App.java");

        var result = await _service.RunTestsAsync();

        Assert.That(result, Does.StartWith("No build tool"));
        Assert.That(_runner.Commands, Is.Empty);
    }

    [Test]
    public async Task NoSources_ReportsError()
    {
        var result = await _service.BuildAsync();

        Assert.That(result, Does.StartWith("ERROR: No Java source files"));
    }
}
