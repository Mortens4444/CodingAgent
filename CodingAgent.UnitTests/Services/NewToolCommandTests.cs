using CodingAgent.Commands;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.Configuration;
using Mocklis;
using System.Text.Json;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class NewToolCommandTests
{
    private TempWorkspace _workspace = null!;
    private MockCodingLanguageService _languageService = null!;
    private TextReader _originalIn = null!;
    private TextWriter _originalOut = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _languageService = new MockCodingLanguageService();
        _languageService.Root.Return(_workspace.Root);
        _originalIn = Console.In;
        _originalOut = Console.Out;
        Console.SetOut(new StringWriter());
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetIn(_originalIn);
        Console.SetOut(_originalOut);
        _workspace.Dispose();
    }

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private string Read(string relativePath) => File.ReadAllText(Path.Combine(_workspace.Root, relativePath));

    private Task<string> Replace(object args) => new ReplaceInFileCommand().ExecuteAsync(Args(args), _languageService);

    [Test]
    public async Task ReplaceInFile_ReplacesUniqueTextAndShowsExcerpt()
    {
        _workspace.Write("a.cs", "line1\nint x = 1;\nline3\n");

        var result = await Replace(new { path = "a.cs", old_text = "int x = 1;", new_text = "int x = 2;" });

        Assert.That(result, Does.StartWith("EDIT SUCCESS: a.cs: replaced 1 occurrence(s), first at line 2."));
        Assert.That(result, Does.Contain("    2: int x = 2;"));
        Assert.That(Read("a.cs"), Is.EqualTo("line1\nint x = 2;\nline3\n"));
    }

    [Test]
    public async Task ReplaceInFile_WrongIndentation_MatchesAndKeepsFileIndentation()
    {
        _workspace.Write("a.cs", "class A\r\n{\r\n    void M()\r\n    {\r\n        Old();\r\n    }\r\n}\r\n");

        var result = await Replace(new { path = "a.cs", old_text = "void M()\n{\n    Old();\n}", new_text = "void M()\n{\n    New();\n    More();\n}" });

        Assert.That(result, Does.StartWith("EDIT SUCCESS: a.cs: replaced 1 occurrence at line 3 (matched ignoring indentation"));
        Assert.That(Read("a.cs"), Is.EqualTo("class A\r\n{\r\n    void M()\r\n    {\r\n        New();\r\n        More();\r\n    }\r\n}\r\n"));
    }

    [Test]
    public async Task ReplaceInFile_WrongIndentationMatchingTwice_IsRejected()
    {
        _workspace.Write("a.cs", "  x();\n    x();\n");

        var result = await Replace(new { path = "a.cs", old_text = " x(); ", new_text = "y();" });

        Assert.That(result, Does.StartWith("ERROR: old_text was not found exactly in a.cs, and it matches 2 places"));
        Assert.That(Read("a.cs"), Is.EqualTo("  x();\n    x();\n"));
    }

    [Test]
    public async Task ReplaceInFile_AcceptsAliasesAndEmptyReplacement()
    {
        _workspace.Write("a.txt", "keep remove keep");

        var result = await Replace(new { file_path = "a.txt", old_str = " remove", new_str = "" });

        Assert.That(result, Does.StartWith("EDIT SUCCESS"));
        Assert.That(Read("a.txt"), Is.EqualTo("keep keep"));
    }

    [Test]
    public async Task ReplaceInFile_AmbiguousText_RequiresReplaceAll()
    {
        _workspace.Write("a.txt", "x\ny\nx\n");

        var rejected = await Replace(new { path = "a.txt", old_text = "x", new_text = "z" });
        var allowed = await Replace(new { path = "a.txt", old_text = "x", new_text = "z", replace_all = true });

        Assert.That(rejected, Does.Contain("occurs 2 times in a.txt (lines 1, 3)"));
        Assert.That(allowed, Does.Contain("replaced 2 occurrence(s)"));
        Assert.That(Read("a.txt"), Is.EqualTo("z\ny\nz\n"));
    }

    [Test]
    public async Task ReplaceInFile_LfSnippetMatchesCrlfFileAndKeepsCrlf()
    {
        _workspace.Write("a.cs", "a\r\nb\r\nc\r\n");

        await Replace(new { path = "a.cs", old_text = "a\nb", new_text = "a\nB\nb2" });

        Assert.That(Read("a.cs"), Is.EqualTo("a\r\nB\r\nb2\r\nc\r\n"));
    }

    [Test]
    public async Task ReplaceInFile_KeepsUtf8Bom()
    {
        var path = Path.Combine(_workspace.Root, "bom.cs");
        File.WriteAllText(path, "old", new System.Text.UTF8Encoding(true));

        await Replace(new { path = "bom.cs", old_text = "old", new_text = "new" });

        Assert.That(File.ReadAllBytes(path).Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [TestCase("{}", "ERROR: Missing required argument 'path'.")]
    [TestCase("{\"path\":\"a.txt\"}", "ERROR: replace_in_file requires 'old_text'")]
    [TestCase("{\"path\":\"a.txt\",\"old_text\":\"a\"}", "ERROR: replace_in_file requires 'new_text'")]
    [TestCase("{\"path\":\"missing.txt\",\"old_text\":\"a\",\"new_text\":\"b\"}", "ERROR: File not found: missing.txt")]
    [TestCase("{\"path\":\"a.txt\",\"old_text\":\"absent\",\"new_text\":\"b\"}", "ERROR: old_text was not found")]
    public async Task ReplaceInFile_InvalidCalls_ReturnErrors(string json, string expected)
    {
        _workspace.Write("a.txt", "abc");

        var result = await new ReplaceInFileCommand().ExecuteAsync(JsonDocument.Parse(json).RootElement, _languageService);

        Assert.That(result, Does.StartWith(expected));
        Assert.That(Read("a.txt"), Is.EqualTo("abc"));
    }

    [Test]
    public async Task DeleteFile_DeletesFilesOnly()
    {
        _workspace.Write("a.txt");
        Directory.CreateDirectory(Path.Combine(_workspace.Root, "dir"));
        var command = new DeleteFileCommand();

        Assert.That(await command.ExecuteAsync(Args(new { path = "a.txt" }), _languageService), Is.EqualTo("DELETE SUCCESS: a.txt was deleted."));
        Assert.That(File.Exists(Path.Combine(_workspace.Root, "a.txt")), Is.False);
        Assert.That(await command.ExecuteAsync(Args(new { path = "a.txt" }), _languageService), Does.StartWith("ERROR: File not found"));
        Assert.That(await command.ExecuteAsync(Args(new { path = "dir" }), _languageService), Does.StartWith("ERROR: delete_file only deletes files"));
    }

    [Test]
    public async Task MoveFile_MovesIntoNewFolderAndRefusesOverwrite()
    {
        _workspace.Write("a.txt", "A");
        _workspace.Write("b.txt", "B");
        var command = new MoveFileCommand();

        var moved = await command.ExecuteAsync(Args(new { source = "a.txt", destination = "sub/a.txt" }), _languageService);
        var overwrite = await command.ExecuteAsync(Args(new { from = "b.txt", to = "sub/a.txt" }), _languageService);
        var missing = await command.ExecuteAsync(Args(new { source = "nope.txt", destination = "x.txt" }), _languageService);

        Assert.That(moved, Is.EqualTo("MOVE SUCCESS: a.txt -> sub/a.txt"));
        Assert.That(Read("sub/a.txt"), Is.EqualTo("A"));
        Assert.That(overwrite, Does.StartWith("ERROR: Destination already exists"));
        Assert.That(missing, Does.StartWith("ERROR: Source file not found"));
    }

    [Test]
    public async Task CheckSyntax_UsesLanguageServiceOrPointsToBuild()
    {
        _languageService.ValidateSyntaxAsync.Return(Task.FromResult("Syntax OK"));
        Assert.That(await new CheckSyntaxCommand().ExecuteAsync(Args(new { }), _languageService), Is.EqualTo("Syntax OK"));

        _languageService.ValidateSyntaxAsync.Return(Task.FromResult(""));
        Assert.That(await new CheckSyntaxCommand().ExecuteAsync(Args(new { }), _languageService), Does.Contain("use build_project"));
    }

    private static IConfiguration Approval(string mode) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["CodingAgent:RunCommand:Approval"] = mode })
        .Build();

    [Test]
    public async Task RunCommand_Ask_RunsOnlyWhenUserAgrees()
    {
        var runner = new RecordingProcessRunner();
        var command = new RunCommandCommand(new ConfigurationBuilder().Build(), runner.Mock);

        Console.SetIn(new StringReader("y\n"));
        var allowed = await command.ExecuteAsync(Args(new { command = "npm", args = new[] { "install", "left pad" } }), _languageService);
        Console.SetIn(new StringReader("\n"));
        var denied = await command.ExecuteAsync(Args(new { command = "npm test" }), _languageService);

        Assert.That(allowed, Does.Contain("Exit code: 0"));
        Assert.That(denied, Does.StartWith("DENIED: The user did not allow 'npm test'"));
        Assert.That(runner.Commands, Is.EqualTo(new[] { "npm install left pad" }));
    }

    [Test]
    public async Task RunCommand_Always_ParsesCommandLine()
    {
        var runner = new RecordingProcessRunner();

        await new RunCommandCommand(Approval("Always"), runner.Mock)
            .ExecuteAsync(Args(new { command = "git commit -m \"first commit\"" }), _languageService);

        Assert.That(runner.Commands, Is.EqualTo(new[] { "git commit -m first commit" }));
    }

    [Test]
    public async Task RunCommand_NeverOrMissingCommandOrMissingTool_ReturnsErrors()
    {
        var runner = new RecordingProcessRunner();
        runner.Missing.Add("sbcl");

        var never = await new RunCommandCommand(Approval("Never"), runner.Mock).ExecuteAsync(Args(new { command = "ls" }), _languageService);
        var empty = await new RunCommandCommand(Approval("Always"), runner.Mock).ExecuteAsync(Args(new { command = " " }), _languageService);
        var missing = await new RunCommandCommand(Approval("Always"), runner.Mock).ExecuteAsync(Args(new { cmd = "sbcl --version" }), _languageService);

        Assert.That(never, Does.StartWith("ERROR: run_command is disabled"));
        Assert.That(empty, Does.StartWith("ERROR: run_command requires 'command'"));
        Assert.That(missing, Does.StartWith("ERROR: 'sbcl' was not found on PATH"));
        Assert.That(runner.Commands, Is.Empty);
    }

    [TestCase("a b  c", new[] { "a", "b", "c" })]
    [TestCase("say \"hello world\" 'x y'", new[] { "say", "hello world", "x y" })]
    [TestCase("empty \"\" end", new[] { "empty", "", "end" })]
    [TestCase("   ", new string[0])]
    public void SplitCommandLine_HandlesQuotes(string line, string[] expected)
    {
        Assert.That(RunCommandCommand.SplitCommandLine(line), Is.EqualTo(expected));
    }
}
