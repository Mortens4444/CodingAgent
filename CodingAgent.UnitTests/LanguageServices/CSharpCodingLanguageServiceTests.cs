using CodingAgent.Services.LanguageServices;
using CodingAgent.UnitTests.Mocks;

namespace CodingAgent.UnitTests.LanguageServices;

[TestFixture]
public class CSharpCodingLanguageServiceTests
{
    private TempWorkspace _workspace = null!;
    private RecordingProcessRunner _runner = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _runner = new RecordingProcessRunner();
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [Test]
    public async Task Build_PassesWarningsAsErrorsAndProjectFile()
    {
        await new CSharpCodingLanguageService(_workspace.Root, "App.slnx", _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "dotnet build --configuration Debug --no-incremental -warnaserror App.slnx" }));
    }

    [Test]
    public async Task Build_OwnSourceTree_RedirectsArtifacts()
    {
        await new CSharpCodingLanguageService(AppContext.BaseDirectory, String.Empty, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands.Single(), Does.Contain("--artifacts-path"));
    }

    [Test]
    public async Task RunTests_ReportsCoverageFromCoberturaReport()
    {
        _runner.Result = (_, args) =>
        {
            var directory = args[args.ToList().IndexOf("--results-directory") + 1];
            Directory.CreateDirectory(Path.Combine(directory, "run"));
            File.WriteAllText(Path.Combine(directory, "run", "coverage.cobertura.xml"),
                "<coverage line-rate=\"0.8125\" branch-rate=\"0.5\"></coverage>");
            return RecordingProcessRunner.Success("Passed!");
        };

        var result = await new CSharpCodingLanguageService(_workspace.Root, String.Empty, _runner.Mock).RunTestsAsync();

        Assert.That(result, Does.Contain("Passed!"));
        Assert.That(result, Does.Contain("Coverage:"));
        Assert.That(result, Does.Contain("lines"));
    }

    [Test]
    public async Task RunTests_WithoutCoverageReport_ExplainsCollectorIsNeeded()
    {
        var result = await new CSharpCodingLanguageService(_workspace.Root, String.Empty, _runner.Mock).RunTestsAsync();

        Assert.That(result, Does.Contain("Coverage: not collected"));
    }

    [Test]
    public async Task RunTests_WithBrokenReport_SaysItCouldNotBeRead()
    {
        _runner.Result = (_, args) =>
        {
            var directory = args[args.ToList().IndexOf("--results-directory") + 1];
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "coverage.cobertura.xml"), "<coverage");
            return RecordingProcessRunner.Success("");
        };

        var result = await new CSharpCodingLanguageService(_workspace.Root, String.Empty, _runner.Mock).RunTestsAsync();

        Assert.That(result, Does.Contain("Coverage: could not read the report"));
    }

    [Test]
    public async Task ValidateSyntax_Builds()
    {
        await new CSharpCodingLanguageService(_workspace.Root, String.Empty, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(_runner.Commands.Single(), Does.StartWith("dotnet build"));
    }
}
