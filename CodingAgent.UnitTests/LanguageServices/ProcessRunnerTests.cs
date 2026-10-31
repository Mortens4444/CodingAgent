using CodingAgent.Services.LanguageServices;
using System.ComponentModel;

namespace CodingAgent.UnitTests.LanguageServices;

// Uses the dotnet CLI, which is always present where these tests run.
[TestFixture]
public class ProcessRunnerTests
{
    [Test]
    public void FindExecutable_FindsDotnetOnPath()
    {
        var path = ProcessRunner.Default.FindExecutable("dotnet");

        Assert.That(path, Is.Not.Null);
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void FindExecutable_AcceptsFullPath()
    {
        var dotnet = ProcessRunner.Default.FindExecutable("dotnet")!;

        Assert.That(ProcessRunner.Default.FindExecutable(dotnet), Is.EqualTo(dotnet));
    }

    [TestCase("")]
    [TestCase("codingagent-no-such-tool")]
    public void FindExecutable_UnknownTool_ReturnsNull(string name)
    {
        Assert.That(ProcessRunner.Default.FindExecutable(name), Is.Null);
    }

    [Test]
    public async Task RunAsync_CapturesExitCodeAndOutput()
    {
        var result = await ProcessRunner.Default.RunAsync("dotnet", Path.GetTempPath(), ["--version"], "dotnet --version");

        Assert.That(result, Does.StartWith("Command: dotnet --version\nExit code: 0\n"));
        Assert.That(result, Does.Match(@"\d+\.\d+"));
    }

    [Test]
    public async Task RunAsync_ReportsNonZeroExitCode()
    {
        var result = await ProcessRunner.Default.RunAsync("dotnet", Path.GetTempPath(), ["codingagent-unknown-command"], "dotnet x");

        Assert.That(result, Does.Not.Contain("Exit code: 0\n"));
    }

    [Test]
    public async Task RunAsync_StopsProcessAfterTimeout()
    {
        var runner = new ProcessRunner(TimeSpan.FromMilliseconds(1));

        var result = await runner.RunAsync("dotnet", Path.GetTempPath(), ["--info"], "dotnet --info");

        Assert.That(result, Does.Contain("timed out"));
    }

    [Test]
    public async Task RunAsync_MissingTool_ThrowsWin32Exception()
    {
        await Assert.ThrowsAsync<Win32Exception>(() =>
            ProcessRunner.Default.RunAsync("codingagent-no-such-tool", Path.GetTempPath(), [], "x"));
    }
}
