using CodingAgent.Commands;
using CodingAgent.Services.LanguageServices;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class CommandTests
{
    private string _tempWorkspace = null!;
    private CSharpCodingLanguageService _languageService = null!;
    private WriteFileCommand _writeFile = null!;

    [SetUp]
    public void Setup()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "CommandTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempWorkspace);
        _languageService = new CSharpCodingLanguageService(_tempWorkspace, String.Empty);
        _writeFile = new WriteFileCommand(new ConfigurationBuilder().Build());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempWorkspace))
            Directory.Delete(_tempWorkspace, true);
    }

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private string WriteWorkspaceFile(string name, string content)
    {
        var path = Path.Combine(_tempWorkspace, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateLines(int count) =>
        String.Join("\n", Enumerable.Range(1, count).Select(i => $"line {i:D4} " + new string('x', 40)));

    [Test]
    public async Task WriteFile_WithoutContent_RejectsAndKeepsFile()
    {
        var path = WriteWorkspaceFile("Foo.cs", "int x = 1;");

        var result = await _writeFile.ExecuteAsync(Args(new { path = "Foo.cs", old_str = "x = 1", new_str = "x = 2" }), _languageService);

        Assert.That(result, Does.StartWith("ERROR: write_file requires 'content'"));
        Assert.That(File.ReadAllText(path), Is.EqualTo("int x = 1;"));
    }

    [Test]
    public async Task WriteFile_WithNonStringContent_Rejects()
    {
        WriteWorkspaceFile("Foo.cs", "int x = 1;");

        var result = await _writeFile.ExecuteAsync(Args(new { path = "Foo.cs", content = new[] { "a", "b" } }), _languageService);

        Assert.That(result, Does.StartWith("ERROR:"));
    }

    [Test]
    public async Task WriteFile_WithEmptyStringContent_CreatesEmptyFile()
    {
        var result = await _writeFile.ExecuteAsync(Args(new { path = "Empty.txt", content = "" }), _languageService);

        Assert.That(result, Does.StartWith("WRITE SUCCESS"));
        Assert.That(File.ReadAllText(Path.Combine(_tempWorkspace, "Empty.txt")), Is.Empty);
    }

    [Test]
    public async Task WriteFile_ShrinkingLargeFile_RejectsUnlessAllowed()
    {
        var original = CreateLines(400);
        var path = WriteWorkspaceFile("Big.cs", original);
        var truncated = original[..(Constants.ReadFilePageLength + 10)];

        var rejected = await _writeFile.ExecuteAsync(Args(new { path = "Big.cs", content = truncated }), _languageService);
        Assert.That(rejected, Does.StartWith("ERROR: The new content is less than half"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(original));

        var allowed = await _writeFile.ExecuteAsync(Args(new { path = "Big.cs", content = truncated, allow_shrink = true }), _languageService);
        Assert.That(allowed, Does.StartWith("WRITE SUCCESS"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(truncated));
    }

    [Test]
    public async Task ReadFile_SmallFile_ReturnsWholeContent()
    {
        WriteWorkspaceFile("Small.cs", "class A {}");

        var result = await new ReadFileCommand().ExecuteAsync(Args(new { path = "Small.cs" }), _languageService);

        Assert.That(result, Is.EqualTo("class A {}"));
    }

    [Test]
    public async Task ReadFile_LargeFile_ReturnsPagesThatCoverEveryLine()
    {
        WriteWorkspaceFile("Big.cs", CreateLines(400));
        var command = new ReadFileCommand();

        var seen = new HashSet<string>();
        var startLine = 1;
        for (var page = 0; page < 20; page++)
        {
            var result = await command.ExecuteAsync(Args(new { path = "Big.cs", start_line = startLine }), _languageService);
            Assert.That(result.Length, Is.LessThan(Constants.MaxToolResultLength));

            var lines = result.Split('\n');
            Assert.That(lines[0], Does.StartWith($"[Lines {startLine}-"));
            foreach (var line in lines.Skip(1)) seen.Add(line);

            if (lines[0].Contains("End of file.")) break;
            startLine = Int32.Parse(Regex.Match(lines[0], "\"start_line\":(\\d+)").Groups[1].Value);
        }

        Assert.That(seen, Has.Count.EqualTo(400));
    }

    [Test]
    public async Task ReadFile_LineNumberAlias_StartsAtThatLine()
    {
        WriteWorkspaceFile("Big.cs", CreateLines(400));

        var result = await new ReadFileCommand().ExecuteAsync(Args(new { path = "Big.cs", line_number = 300 }), _languageService);

        Assert.That(result, Does.StartWith("[Lines 300-"));
        Assert.That(result.Split('\n')[1], Does.StartWith("line 0300"));
    }

    [Test]
    public async Task CheckForMissingFiles_AcceptsSinglePath()
    {
        WriteWorkspaceFile("Program.cs", "");

        var result = await new CheckForMissingFilesCommand().ExecuteAsync(Args(new { path = "Program.cs" }), _languageService);

        Assert.That(result, Is.EqualTo("EXISTS: Program.cs"));
    }

    [Test]
    public async Task SearchText_WildcardPattern_MatchesFileNames()
    {
        WriteWorkspaceFile("FooTests.cs", "nothing relevant");
        WriteWorkspaceFile("Foo.cs", "nothing relevant");

        var result = await new SearchTextCommand().ExecuteAsync(Args(new { pattern = "*Tests.cs" }), _languageService);

        Assert.That(result, Does.Contain("Matches: 1"));
        Assert.That(result, Does.Contain("FooTests.cs"));
    }
}
