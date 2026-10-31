using CodingAgent.Commands;
using CodingAgent.Enums;
using CodingAgent.Services;
using CodingAgent.Services.DevEnvironment;
using CodingAgent.Services.LanguageServices;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Mocklis;
using System.Text.Json;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class LineDiffTests
{
    [Test]
    public void IdenticalTexts_HaveNoDiff() =>
        Assert.That(LineDiff.Unified("a\nb\n", "a\nb\n"), Is.Empty);

    [Test]
    public void ChangedLine_ShowsRemovalBeforeAdditionWithContext()
    {
        var diff = LineDiff.Unified("1\n2\n3\n4\n5\n6\n7\n", "1\n2\n3\nfour\n5\n6\n7\n", context: 1);

        Assert.That(diff, Is.EqualTo(new[] { "@@ -3 +3 @@", " 3", "-4", "+four", " 5" }));
    }

    [Test]
    public void DistantChanges_AreSeparateHunks()
    {
        var oldText = String.Join("\n", Enumerable.Range(1, 20));
        var newText = oldText.Replace("\n2\n", "\nTWO\n").Replace("\n19\n", "\nNINETEEN\n");

        var diff = LineDiff.Unified(oldText, newText, context: 1);

        Assert.That(diff.Count(line => line.StartsWith("@@")), Is.EqualTo(2));
    }

    [Test]
    public void NewAndDeletedFiles_AreAllAdditionsOrRemovals()
    {
        Assert.That(LineDiff.Unified(null, "a\r\nb"), Is.EqualTo(new[] { "@@ -1 +1 @@", "+a", "+b" }));
        Assert.That(LineDiff.Unified("a\n", null), Is.EqualTo(new[] { "@@ -1 +1 @@", "-a" }));
    }

    [Test]
    public void LongDiff_IsTruncated()
    {
        var diff = LineDiff.Unified(null, String.Join("\n", Enumerable.Range(1, 50)), maxLines: 10);

        Assert.That(diff, Has.Count.EqualTo(11));
        Assert.That(diff[^1], Is.EqualTo("@@ 41 more diff lines not shown @@"));
    }

    [Test]
    public void HugeFiles_AreOnlySummarized()
    {
        var text = String.Join("\n", Enumerable.Range(1, 3000));

        Assert.That(LineDiff.Unified(text, text + "\nx"), Is.EqualTo(new[] { "@@ too large to compare line by line: 3000 -> 3001 lines @@" }));
    }
}

[TestFixture]
public class FileChangeTests
{
    private TempWorkspace _workspace = null!;
    private MockCodingLanguageService _languageService = null!;
    private TextWriter _originalOut = null!;
    private StringWriter _out = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _languageService = new MockCodingLanguageService();
        _languageService.Root.Return(_workspace.Root);
        _originalOut = Console.Out;
        _out = new StringWriter();
        Console.SetOut(_out);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        _out.Dispose();
        _workspace.Dispose();
    }

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private static IConfiguration Config(string approval) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["CodingAgent:FileChanges:Approval"] = approval })
        .Build();

    private string Path(string relativePath) => System.IO.Path.Combine(_workspace.Root, relativePath);

    [Test]
    public async Task Tracker_ReportsCreatedModifiedDeletedAndMovedFiles()
    {
        _workspace.Write("edit.txt", "old\n");
        _workspace.Write("gone.txt", "bye\n");
        _workspace.Write("same.txt", "same\n");
        _workspace.Write("from.txt", "moved\n");
        var dispatcher = new ToolDispatcher(Config("Always"));

        await dispatcher.ExecuteToolAsync(_languageService, "write_file", Args(new { path = "new.txt", content = "hello\n" }));
        await dispatcher.ExecuteToolAsync(_languageService, "replace_in_file", Args(new { path = "edit.txt", old_text = "old", new_text = "new" }));
        await dispatcher.ExecuteToolAsync(_languageService, "replace_in_file", Args(new { path = "edit.txt", old_text = "new", new_text = "newer" }));
        await dispatcher.ExecuteToolAsync(_languageService, "delete_file", Args(new { path = "gone.txt" }));
        await dispatcher.ExecuteToolAsync(_languageService, "write_file", Args(new { path = "same.txt", content = "same\n" }));
        await dispatcher.ExecuteToolAsync(_languageService, "move_file", Args(new { source = "from.txt", destination = "sub/to.txt" }));

        var changes = dispatcher.Changes.GetChanges();

        Assert.That(changes.Select(change => $"{change.Status} {change.Path}"), Is.EqualTo(new[]
        {
            "modified edit.txt", "deleted from.txt", "deleted gone.txt", "created new.txt", "created sub/to.txt"
        }));
        Assert.That(changes[0].Diff, Is.EqualTo(new[] { "@@ -1 +1 @@", "-old", "+newer" }));

        dispatcher.Changes.WriteReport();
        Assert.That(_out.ToString(), Does.Contain("=== Changed files (5) ===").And.Contain("--- edit.txt (modified)"));
    }

    [Test]
    public void Tracker_WithoutChanges_WritesNothing()
    {
        new FileChangeTracker().WriteReport();

        Assert.That(_out.ToString(), Is.Empty);
    }

    [Test]
    public void Tracker_IgnoresPathsOutsideTheRoot()
    {
        var tracker = new FileChangeTracker();
        tracker.Snapshot(_workspace.Root, ["../outside.txt", "C:/abs.txt"]);

        Assert.That(tracker.GetChanges(), Is.Empty);
    }

    [Test]
    public async Task ApprovalNever_DeniesChangesWithoutWriting()
    {
        var dispatcher = new ToolDispatcher(Config("Never"));

        var result = await dispatcher.ExecuteToolAsync(_languageService, "write_file", Args(new { path = "a.txt", content = "x" }));

        Assert.That(result, Does.StartWith("DENIED: File changes are disabled"));
        Assert.That(File.Exists(Path("a.txt")), Is.False);
    }

    [Test]
    public async Task ApprovalAsk_ShowsPreviewAndHonorsTheAnswer()
    {
        _workspace.Write("a.txt", "one\ntwo\n");
        var questions = new List<string>();
        var answer = false;
        var dispatcher = new ToolDispatcher(Config("Ask"), askUser: question => { questions.Add(question); return answer; });
        var args = Args(new { path = "a.txt", old_text = "two", new_text = "2" });

        var denied = await dispatcher.ExecuteToolAsync(_languageService, "replace_in_file", args);
        answer = true;
        var allowed = await dispatcher.ExecuteToolAsync(_languageService, "replace_in_file", args);

        Assert.That(denied, Does.StartWith("DENIED: The user did not allow this replace_in_file call"));
        Assert.That(allowed, Does.StartWith("EDIT SUCCESS"));
        Assert.That(questions, Has.Count.EqualTo(2));
        Assert.That(File.ReadAllText(Path("a.txt")), Is.EqualTo("one\n2\n"));
        Assert.That(_out.ToString(), Does.Contain("The agent wants to edit a.txt:").And.Contain("-two").And.Contain("+2"));
    }

    [Test]
    public async Task ApprovalAsk_DoesNotAskForReadOnlyTools()
    {
        _workspace.Write("a.txt", "x");
        var dispatcher = new ToolDispatcher(Config("Ask"), askUser: _ => throw new AssertionException("asked"));

        var result = await dispatcher.ExecuteToolAsync(_languageService, "read_file", Args(new { path = "a.txt" }));

        Assert.That(result, Does.Contain("x"));
    }

    [Test]
    public void Describe_CoversEveryChangingTool()
    {
        _workspace.Write("old.txt", "a\n");

        Assert.That(FileChangeApproval.Describe(_workspace.Root, "write_file", Args(new { path = "old.txt", content = "b\n" })).Title, Is.EqualTo("overwrite old.txt:"));
        Assert.That(FileChangeApproval.Describe(_workspace.Root, "write_file", Args(new { file_path = "new.txt", content = "b\n" })).Preview, Does.Contain("+b"));
        Assert.That(FileChangeApproval.Describe(_workspace.Root, "delete_file", Args(new { path = "old.txt" })).Title, Is.EqualTo("delete old.txt"));
        Assert.That(FileChangeApproval.Describe(_workspace.Root, "move_file", Args(new { from = "old.txt", to = "b.txt" })).Title, Is.EqualTo("move old.txt -> b.txt"));
        Assert.That(FileChangeApproval.Describe(_workspace.Root, "move_file", Args(new { })).Title, Is.EqualTo("move (no path) -> (no destination)"));
        Assert.That(FileChangeApproval.Describe(_workspace.Root, "other", Args(new { path = "x" })).Title, Is.EqualTo("run other on x"));
    }

    [Test]
    public void UserApproval_AcceptsOnlyYes()
    {
        var originalIn = Console.In;
        Console.SetIn(new StringReader("yes\nn\n"));
        try
        {
            Assert.That(UserApproval.Ask("Q?"), Is.True);
            Assert.That(UserApproval.Ask("Q?"), Is.False);
            Assert.That(UserApproval.Ask("Q?"), Is.False);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }
}

[TestFixture]
public class CheckEnvironmentTests
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

    private Task<string> Run(CodingAgent.Interfaces.ICodingLanguageService service) =>
        new ToolDispatcher(new ConfigurationBuilder().Build(), _runner.Mock).ExecuteToolAsync(service, "check_environment", default);

    [Test]
    public async Task AllToolsPresent_ReportsReady()
    {
        var result = await Run(new LispCodingLanguageService(_workspace.Root, _runner.Mock));

        Assert.That(result, Does.StartWith("Development environment for Lisp:"));
        Assert.That(result, Does.Contain("ENVIRONMENT READY"));
    }

    [Test]
    public async Task MissingTool_ReportsIncomplete()
    {
        _runner.Missing.Add("sbcl");

        var result = await Run(new LispCodingLanguageService(_workspace.Root, _runner.Mock));

        Assert.That(result, Does.Contain("[MISSING]").And.Contain("ENVIRONMENT INCOMPLETE: Missing required tools on this machine: sbcl"));
    }

    [Test]
    public void LanguageOf_MapsEveryLanguageService()
    {
        var root = _workspace.Root;
        var runner = _runner.Mock;

        Assert.Multiple(() =>
        {
            Assert.That(CheckEnvironmentCommand.LanguageOf(new CSharpCodingLanguageService(root, "a.csproj", runner)), Is.EqualTo(ProgrammingLanguage.CSharp));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new PythonCodingLanguagetService(root, runner)), Is.EqualTo(ProgrammingLanguage.Python));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new CCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.C));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new CppCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.Cpp));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new JavaCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.Java));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new JavaScriptCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.JavaScript));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new TypeScriptCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.TypeScript));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new HtmlCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.Html));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new AssemblyCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.Assembly));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new PascalCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.Pascal));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new LispCodingLanguageService(root, runner)), Is.EqualTo(ProgrammingLanguage.Lisp));
            Assert.That(CheckEnvironmentCommand.LanguageOf(new MockCodingLanguageService()), Is.EqualTo(ProgrammingLanguage.Unknown));
        });
    }

    [Test]
    public void ToolAliases_MapToCheckEnvironment() =>
        Assert.That(ToolCatalog.NormalizeToolName("check_env"), Is.EqualTo("check_environment"));

    [Test]
    public void MergePath_AddsOnlyNewEntriesAndKeepsOrder()
    {
        var separator = System.IO.Path.PathSeparator;
        var current = String.Join(separator, "/a", "/b/");
        var stored = String.Join(separator, "/b", "\"/a\"", "/c", "/c");

        var (path, added) = DevEnvironmentChecker.MergePath(current, stored);

        Assert.That(added, Is.EqualTo(1));
        Assert.That(path, Is.EqualTo(String.Join(separator, "/a", "/b/", "/c")));
    }

    [Test]
    public void RefreshPath_KeepsTheCurrentEntries()
    {
        var before = Environment.GetEnvironmentVariable("PATH");

        DevEnvironmentChecker.RefreshPath();

        Assert.That(Environment.GetEnvironmentVariable("PATH"), Does.StartWith(before));
    }

    [Test]
    public void SetupCommand_MatchesTheOperatingSystem()
    {
        var command = ToolchainCatalog.SetupCommand(ProgrammingLanguage.C);

        Assert.That(command, Is.EqualTo(OperatingSystem.IsWindows()
            ? "scripts/setup-dev-env.ps1 -Languages C"
            : "scripts/setup-dev-env.sh C"));
    }
}

[TestFixture]
public class StreamingLoopTests
{
    private TempWorkspace _workspace = null!;
    private MockChatClientForLoop _chatClient = null!;
    private MockCodingLanguageServiceForLoop _languageService = null!;
    private TextWriter _originalOut = null!;
    private StringWriter _out = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _chatClient = new MockChatClientForLoop();
        _languageService = new MockCodingLanguageServiceForLoop();
        _languageService.Root.Return(_workspace.Root);
        _languageService.BuildAsync.Return(Task.FromResult("Command: build\nExit code: 0\nok"));
        _originalOut = Console.Out;
        _out = new StringWriter();
        Console.SetOut(_out);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        _out.Dispose();
        ((IDisposable)_chatClient).Dispose();
        _workspace.Dispose();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Chunks(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Failing()
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "{\"na");
        throw new HttpRequestException("connection reset");
    }

    [Test]
    public async Task Streaming_PrintsChunksAndUsesTheJoinedReply()
    {
        var replies = new Queue<IAsyncEnumerable<ChatResponseUpdate>>(
        [
            Chunks("{\"name\":\"write_file\",", "\"arguments\":{\"path\":\"a.txt\",\"content\":\"hi\"}}"),
            Chunks("{\"name\":\"build_project\",\"arguments\":{}}"),
            Chunks("{\"name\":\"finish\",", "\"arguments\":{\"summary\":\"done streaming\"}}")
        ]);
        _chatClient.GetStreamingResponseAsync.Func(_ => replies.Dequeue());
        _chatClient.GetResponseAsync.Func(_ => throw new AssertionException("non-streaming call"));
        var loop = new CodingAgentLoop(_chatClient, new ToolDispatcher(new ConfigurationBuilder().Build()), streamOutput: true);

        await loop.RunAsync(_languageService, [new(ChatRole.System, "system"), new(ChatRole.User, "task")]);

        var output = _out.ToString();
        Assert.That(output, Does.Contain("{\"name\":\"write_file\",\"arguments\":{\"path\":\"a.txt\",\"content\":\"hi\"}}"));
        Assert.That(output, Does.Contain("done streaming"));
        Assert.That(output, Does.Contain("=== Changed files (1) ===").And.Contain("+hi"));
        Assert.That(File.ReadAllText(Path.Combine(_workspace.Root, "a.txt")), Is.EqualTo("hi"));
    }

    [Test]
    public async Task Streaming_ErrorMidReply_StopsGracefully()
    {
        _chatClient.GetStreamingResponseAsync.Func(_ => Failing());
        var loop = new CodingAgentLoop(_chatClient, new ToolDispatcher(new ConfigurationBuilder().Build()), streamOutput: true);

        await loop.RunAsync(_languageService, [new(ChatRole.System, "system"), new(ChatRole.User, "task")]);

        Assert.That(_out.ToString(), Does.Contain("Error when calling Ollama: connection reset"));
    }
}
