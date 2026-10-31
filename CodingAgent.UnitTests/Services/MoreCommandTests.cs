using CodingAgent.Commands;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.Configuration;
using Mocklis;
using System.Text.Json;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class MoreCommandTests
{
    private TempWorkspace _workspace = null!;
    private MockCodingLanguageService _languageService = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _languageService = new MockCodingLanguageService();
        _languageService.Root.Return(_workspace.Root);
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    [Test]
    public async Task ListFiles_ListsDirectoriesAndFilesWithoutExcludedFolders()
    {
        _workspace.Write("src/App.cs");
        _workspace.Write("README.md");
        _workspace.Write("bin/App.dll");

        var result = await new ListFilesCommand().ExecuteAsync(Args(new { path = "." }), _languageService);

        Assert.That(result, Does.StartWith("Directory: .; entries: 2"));
        Assert.That(result, Does.Contain("[DIR]  src/"));
        Assert.That(result, Does.Contain("[FILE] README.md"));
        Assert.That(result, Does.Not.Contain("bin"));
    }

    [Test]
    public async Task ListFiles_DirectoryAliasAndAbsolutePathInsideRoot()
    {
        _workspace.Write("src/App.cs");

        var alias = await new ListFilesCommand().ExecuteAsync(Args(new { directory = "src" }), _languageService);
        var absolute = await new ListFilesCommand().ExecuteAsync(Args(new { path = Path.Combine(_workspace.Root, "src") }), _languageService);

        Assert.That(alias, Does.Contain("[FILE] src/App.cs"));
        Assert.That(absolute, Does.Contain("[FILE] src/App.cs"));
    }

    [Test]
    public async Task ListFiles_ManyEntries_AreTruncated()
    {
        for (var i = 0; i < 170; i++)
            _workspace.Write($"f{i:D3}.txt");

        var result = await new ListFilesCommand().ExecuteAsync(Args(new { path = "." }), _languageService);

        Assert.That(result, Does.Contain("entries: 170 (showing first 160"));
    }

    [Test]
    public async Task ListFiles_MissingDirectory_ReturnsError()
    {
        var result = await new ListFilesCommand().ExecuteAsync(Args(new { path = "nope" }), _languageService);

        Assert.That(result, Does.StartWith("ERROR:"));
    }

    [Test]
    public async Task ListFiles_OutsideRoot_IsBlocked()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ListFilesCommand().ExecuteAsync(Args(new { path = ".." }), _languageService));
    }

    [Test]
    public async Task SearchText_FindsContentWithLineNumbers()
    {
        _workspace.Write("src/a.c", "int x;\nint needle = 1;\n");
        _workspace.Write("notes.bin", "needle");
        _workspace.Write("node_modules/b.js", "needle");

        var result = await new SearchTextCommand().ExecuteAsync(Args(new { query = "NEEDLE" }), _languageService);

        Assert.That(result, Does.Contain("Matches: 1"));
        Assert.That(result, Does.Contain("src/a.c:2: int needle = 1;"));
    }

    [TestCase("", "ERROR: query must not be empty.")]
    [TestCase("x", "ERROR: Use a search query of at least two characters.")]
    public async Task SearchText_InvalidQuery_ReturnsError(string query, string expected)
    {
        var result = await new SearchTextCommand().ExecuteAsync(Args(new { query }), _languageService);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public async Task SearchText_PatternAlias_NoMatches()
    {
        _workspace.Write("a.txt", "hello");

        var result = await new SearchTextCommand().ExecuteAsync(Args(new { pattern = "absent" }), _languageService);

        Assert.That(result, Does.Contain("No matches found."));
    }

    [Test]
    public async Task SearchText_StopsAtMatchLimit()
    {
        _workspace.Write("a.txt", String.Join("\n", Enumerable.Repeat("hit", 150)));

        var result = await new SearchTextCommand().ExecuteAsync(Args(new { text = "hit" }), _languageService);

        Assert.That(result, Does.Contain("Matches: 100 (limit reached)"));
    }

    [Test]
    public async Task CheckForMissingFiles_ReportsExistingMissingAndBlocked()
    {
        _workspace.Write("a.txt");
        var outside = Path.GetFullPath(Path.Combine(_workspace.Root, "..", "outside.txt"));

        var result = await new CheckForMissingFilesCommand().ExecuteAsync(Args(new
        {
            file_paths = new[] { "a.txt", "b.txt", Path.Combine(_workspace.Root, "a.txt"), outside, "bin/x.dll", "" }
        }), _languageService);

        var lines = result.Split(Environment.NewLine);
        Assert.That(lines[0], Is.EqualTo("EXISTS: a.txt"));
        Assert.That(lines[1], Is.EqualTo("MISSING: b.txt"));
        Assert.That(lines[2], Is.EqualTo("EXISTS: a.txt"));
        Assert.That(lines[3], Does.StartWith("BLOCKED:"));
        Assert.That(lines[4], Does.StartWith("BLOCKED: bin/x.dll"));
        Assert.That(lines[5], Does.StartWith("BLOCKED:"));
    }

    [Test]
    public async Task CheckForMissingFiles_WithoutPaths_ReturnsError()
    {
        var result = await new CheckForMissingFilesCommand().ExecuteAsync(Args(new { other = 1 }), _languageService);
        var notObject = await new CheckForMissingFilesCommand().ExecuteAsync(Args(new[] { 1 }), _languageService);

        Assert.That(result, Does.StartWith("ERROR: check_for_missing_files requires"));
        Assert.That(notObject, Does.StartWith("ERROR: check_for_missing_files requires"));
    }

    [Test]
    public async Task WriteFile_WithBackup_SavesOldVersion()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CodingAgent:WriteFile:Backup"] = "true" })
            .Build();
        _workspace.Write("src/a.txt", "old");

        var result = await new WriteFileCommand(configuration).ExecuteAsync(Args(new { file_path = "src/a.txt", new_content = "new" }), _languageService);

        Assert.That(result, Does.Contain("was updated"));
        Assert.That(result, Does.Contain(".codingagent-backups"));
        Assert.That(File.ReadAllText(Path.Combine(_workspace.Root, "src", "a.txt")), Is.EqualTo("new"));
        var backup = Directory.GetFiles(Path.Combine(_workspace.Root, ".codingagent-backups"), "a.txt", SearchOption.AllDirectories);
        Assert.That(File.ReadAllText(backup.Single()), Is.EqualTo("old"));
    }

    [Test]
    public async Task WriteFile_WithoutPath_ReturnsError()
    {
        var result = await new WriteFileCommand(new ConfigurationBuilder().Build()).ExecuteAsync(Args(new { content = "x" }), _languageService);

        Assert.That(result, Is.EqualTo("ERROR: Missing required argument 'path'."));
    }

    [Test]
    public async Task WriteFile_AbsolutePath_IsBlocked()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WriteFileCommand(new ConfigurationBuilder().Build()).ExecuteAsync(Args(new { path = Path.Combine(_workspace.Root, "a.txt"), content = "x" }), _languageService));
    }

    [Test]
    public async Task BuildAndRunTests_DelegateToLanguageService()
    {
        _languageService.BuildAsync.Return(Task.FromResult("built"));
        _languageService.RunTestsAsync.Return(Task.FromResult("tested"));

        Assert.That(await new BuildProjectCommand().ExecuteAsync(Args(new { }), _languageService), Is.EqualTo("built"));
        Assert.That(await new RunTestsCommand().ExecuteAsync(Args(new { }), _languageService), Is.EqualTo("tested"));
    }
}
