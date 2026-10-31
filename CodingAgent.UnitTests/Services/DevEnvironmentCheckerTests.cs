using CodingAgent.Enums;
using CodingAgent.Services.DevEnvironment;
using CodingAgent.UnitTests.Mocks;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class DevEnvironmentCheckerTests
{
    [Test]
    public void Check_AllToolsPresent_IsReady()
    {
        var runner = new RecordingProcessRunner();

        var report = DevEnvironmentChecker.Check(ProgrammingLanguage.C, runner.Mock);

        Assert.That(report.IsReady, Is.True);
        Assert.That(report.PromptNote, Is.Null);
        Assert.That(report.Text, Does.Contain("[OK]"));
        Assert.That(report.Text, Does.Not.Contain(ToolchainCatalog.SetupScript));
    }

    [Test]
    public void Check_MissingRequiredAndOptionalTools_ListsInstallHints()
    {
        var runner = new RecordingProcessRunner();
        runner.Missing.UnionWith(["gcc", "cmake"]);

        var report = DevEnvironmentChecker.Check(ProgrammingLanguage.C, runner.Mock);

        Assert.That(report.IsReady, Is.False);
        Assert.That(report.MissingRequired.Select(tool => tool.Name), Is.EqualTo(new[] { "gcc" }));
        Assert.That(report.MissingOptional.Select(tool => tool.Name), Is.EqualTo(new[] { "cmake" }));
        Assert.That(report.Text, Does.Contain("[MISSING"));
        Assert.That(report.Text, Does.Contain("[OPTIONAL"));
        Assert.That(report.Text, Does.Contain(ToolchainCatalog.SetupScript + " -Languages C"));
        Assert.That(report.PromptNote, Does.Contain("gcc"));
    }

    [Test]
    public void Check_AlternativeCommandSatisfiesRequirement()
    {
        var runner = new RecordingProcessRunner();
        runner.Missing.Add("python");

        var report = DevEnvironmentChecker.Check(ProgrammingLanguage.Python, runner.Mock);

        Assert.That(report.IsReady, Is.True);
    }

    [Test]
    public void Check_UnknownLanguage_HasNoRequirements()
    {
        var report = DevEnvironmentChecker.Check(ProgrammingLanguage.Unknown, new RecordingProcessRunner().Mock);

        Assert.That(report.Text, Does.Contain("no toolchain requirements known"));
    }

    [Test]
    public void Catalog_EverySupportedLanguageHasRequirements()
    {
        foreach (var language in Enum.GetValues<ProgrammingLanguage>().Where(language => language != ProgrammingLanguage.Unknown))
            Assert.That(ToolchainCatalog.For(language), Is.Not.Empty, language.ToString());
    }

    [Test]
    public void Catalog_EveryToolHasInstallHintsForAllPlatforms()
    {
        var tools = Enum.GetValues<ProgrammingLanguage>().SelectMany(ToolchainCatalog.For);

        foreach (var tool in tools)
        {
            Assert.That(tool.WindowsInstall, Is.Not.Empty, tool.Name);
            Assert.That(tool.LinuxInstall, Is.Not.Empty, tool.Name);
            Assert.That(tool.MacInstall, Is.Not.Empty, tool.Name);
            Assert.That(tool.InstallHint, Is.Not.Empty, tool.Name);
        }
    }

    [TestCase("fpc", "Free Pascal")]
    [TestCase("C:/tools/nasm.exe", "assembler")]
    [TestCase("mingw32-make", "Makefile")]
    public void MissingToolMessage_KnownTool_IncludesPurposeAndInstall(string command, string purpose)
    {
        var message = ToolchainCatalog.MissingToolMessage(command);

        Assert.That(message, Does.StartWith($"ERROR: '{command}' was not found on PATH"));
        Assert.That(message, Does.Contain(purpose));
        Assert.That(message, Does.Contain("Install:"));
    }

    [Test]
    public void MissingToolMessage_UnknownTool_StillPointsToSetupScript()
    {
        var message = ToolchainCatalog.MissingToolMessage("whatever");

        Assert.That(message, Does.Not.Contain("Install:"));
        Assert.That(message, Does.Contain(ToolchainCatalog.SetupScript));
    }
}
