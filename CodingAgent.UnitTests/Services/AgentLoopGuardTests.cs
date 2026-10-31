using CodingAgent.Services;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Mocklis;
using System.Text.Json;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class AgentLoopGuardTests
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

    private CodingAgentLoop CreateLoop(int contextLength = 8192) =>
        new(_chatClient, new ToolDispatcher(new ConfigurationBuilder().Build()), contextLength);

    // Answers with the scripted replies in order, then keeps repeating the last one.
    private List<List<ChatMessage>> Script(params string[] replies)
    {
        var requests = new List<List<ChatMessage>>();
        _chatClient.GetResponseAsync.Func(request =>
        {
            requests.Add(request.messages.ToList());
            var reply = replies[Math.Min(requests.Count - 1, replies.Length - 1)];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        });
        return requests;
    }

    private static List<ChatMessage> Start() => [new(ChatRole.System, "system"), new(ChatRole.User, "task")];

    private static string Call(string name, object args) => JsonSerializer.Serialize(new { name, arguments = args });

    [Test]
    public async Task Finish_AfterUnvalidatedChange_IsRejectedOnce()
    {
        var messages = Start();
        Script(
            Call("write_file", new { path = "a.txt", content = "x" }),
            Call("finish", new { summary = "early" }),
            Call("finish", new { summary = "really done" }));

        await CreateLoop().RunAsync(_languageService, messages);

        Assert.That(messages.Any(m => m.Text.StartsWith("HOST CHECK: Files were changed")), Is.True);
        Assert.That(_out.ToString(), Does.Contain("really done"));
    }

    [Test]
    public async Task Finish_WithoutAnyChange_IsRejectedOnce()
    {
        var messages = Start();
        Script(
            Call("list_files", new { path = "." }),
            Call("finish", new { summary = "looked around" }),
            Call("finish", new { summary = "nothing to change because X" }));

        await CreateLoop().RunAsync(_languageService, messages);

        Assert.That(messages.Count(m => m.Text.StartsWith("HOST CHECK: No file has been changed")), Is.EqualTo(1));
        Assert.That(_out.ToString(), Does.Contain("nothing to change because X"));
    }

    [Test]
    public async Task ManyCallsWithoutChange_AddImplementationReminder()
    {
        var messages = Start();
        var replies = Enumerable.Range(0, 8).Select(i => Call("search_text", new { query = "q" + i }))
            .Append(Call("write_file", new { path = "a.txt", content = "x" }))
            .Append(Call("build_project", new { }))
            .Append(Call("finish", new { summary = "done" }))
            .ToArray();
        Script(replies);

        await CreateLoop().RunAsync(_languageService, messages);

        var reminders = messages.Where(m => m.Text.Contains("HOST NOTE: 8 tool calls and no file has been changed yet")).ToList();
        Assert.That(reminders, Has.Count.EqualTo(1));
        Assert.That(reminders[0].Text, Does.StartWith("Tool result for 'search_text'"));
    }

    [Test]
    public async Task Finish_AfterBuild_IsAccepted()
    {
        var messages = Start();
        Script(
            Call("write_file", new { path = "a.txt", content = "x" }),
            Call("build_project", new { }),
            Call("finish", new { summary = "validated" }));

        await CreateLoop().RunAsync(_languageService, messages);

        Assert.That(messages.Any(m => m.Text.StartsWith("HOST CHECK")), Is.False);
        Assert.That(_out.ToString(), Does.Contain("validated"));
    }

    [Test]
    public async Task IdenticalCalls_AreRefusedOnTheThirdRepeat()
    {
        var messages = Start();
        var listing = Call("list_files", new { path = "." });
        Script(listing, listing, listing, Call("finish", new { summary = "done" }));

        await CreateLoop().RunAsync(_languageService, messages);

        Assert.That(messages.Count(m => m.Text.StartsWith("Tool result for 'list_files'")), Is.EqualTo(2));
        Assert.That(messages.Any(m => m.Text.StartsWith("HOST CHECK: You called list_files with the same arguments 3 times")), Is.True);
    }

    [Test]
    public async Task AlternatingIdenticalCalls_AreRefusedUntilAFileChanges()
    {
        var messages = Start();
        var listing = Call("list_files", new { path = "." });
        var search = Call("search_text", new { query = "x" });
        var write = Call("write_file", new { path = "a.txt", content = "x" });
        Script(listing, search, listing, search, listing, write, listing, Call("build_project", new { }), Call("finish", new { summary = "done" }));

        await CreateLoop().RunAsync(_languageService, messages);

        Assert.That(messages.Count(m => m.Text.StartsWith("HOST CHECK: You called list_files with the same arguments 3 times since the last file change")), Is.EqualTo(1));
        Assert.That(messages.Count(m => m.Text.StartsWith("Tool result for 'list_files'")), Is.EqualTo(3));
    }

    [Test]
    public async Task Finish_AfterFailedBuild_IsRejectedOnce()
    {
        var messages = Start();
        _languageService.BuildAsync.Return(Task.FromResult("Command: build\nExit code: 1\nerror"));
        Script(
            Call("build_project", new { }),
            Call("finish", new { summary = "early" }),
            Call("finish", new { summary = "blocked by build error" }));

        await CreateLoop().RunAsync(_languageService, messages);

        Assert.That(messages.Count(m => m.Text.StartsWith("HOST CHECK: The last build_project")), Is.EqualTo(1));
        Assert.That(_out.ToString(), Does.Contain("blocked by build error"));
    }

    [Test]
    public async Task Trimming_DropsReplyAndResultTogether()
    {
        var messages = Start();
        var big = new string('x', 2500);
        var requests = Script(
            Call("write_file", new { path = "a.txt", content = big }),
            Call("write_file", new { path = "b.txt", content = big }),
            Call("write_file", new { path = "c.txt", content = big }),
            Call("build_project", new { }),
            Call("finish", new { summary = "done" }));

        await CreateLoop(contextLength: 2000).RunAsync(_languageService, messages);

        foreach (var request in requests.Where(r => r.Count > 3 && r[2].Text.StartsWith(AgentProgress.NotePrefix)))
            Assert.That(request[3].Role, Is.EqualTo(ChatRole.Assistant));
    }

    [Test]
    public async Task ConsecutiveUnusableReplies_StopTheRun()
    {
        var requests = Script("no json here");

        await CreateLoop().RunAsync(_languageService, Start());

        Assert.That(requests, Has.Count.EqualTo(5));
        Assert.That(_out.ToString(), Does.Contain("5 unusable responses in a row"));
    }

    [Test]
    public async Task Trimming_KeepsProgressNoteAfterTask()
    {
        var messages = Start();
        var big = new string('x', 2500);
        var requests = Script(
            Call("write_file", new { path = "a.txt", content = big }),
            Call("write_file", new { path = "b.txt", content = big }),
            Call("build_project", new { }),
            Call("finish", new { summary = "done" }));

        await CreateLoop(contextLength: 2000).RunAsync(_languageService, messages);

        var last = requests[^1];
        Assert.That(last[0].Text, Is.EqualTo("system"));
        Assert.That(last[1].Text, Is.EqualTo("task"));
        Assert.That(last[2].Text, Does.StartWith(AgentProgress.NotePrefix));
        Assert.That(last[2].Text, Does.Contain("a.txt, b.txt"));
        Assert.That(last[2].Text, Does.Contain("build_project passed"));
    }

    [Test]
    public void Progress_TracksChangesAndValidation()
    {
        var progress = new AgentProgress();
        Assert.That(progress.IsEmpty, Is.True);

        progress.Record("move_file", JsonSerializer.SerializeToElement(new { source = "a", destination = "b\\c.txt" }), "MOVE SUCCESS: a -> b/c.txt");
        progress.Record("replace_in_file", JsonSerializer.SerializeToElement(new { path = "x.cs" }), "ERROR: old_text was not found");
        Assert.That(progress.HasUnvalidatedChanges, Is.True);
        Assert.That(progress.ToNote(), Does.Contain("Files changed so far: b/c.txt\n"));
        Assert.That(progress.ToNote(), Does.Contain("Files changed since the last build/test."));

        progress.Record("run_tests", default, "Command: t\nExit code: 1\nfailed");
        Assert.That(progress.HasUnvalidatedChanges, Is.False);
        Assert.That(progress.ToNote(), Does.Contain("run_tests failed"));
    }

    [TestCase("Command: a\nExit code: 0\nok", true)]
    [TestCase("Command: a\nExit code: 0\n\nCommand: b\nExit code: 2\n", false)]
    [TestCase("ERROR: 'gcc' was not found on PATH", false)]
    public void Progress_Passed_RequiresEveryExitCodeZero(string result, bool expected)
    {
        Assert.That(AgentProgress.Passed(result), Is.EqualTo(expected));
    }

    [Test]
    public void Condenser_ShortOutput_IsUnchanged()
    {
        Assert.That(ToolOutputCondenser.Condense("short", 100), Is.EqualTo("short"));
    }

    [Test]
    public void Condenser_LongOutput_KeepsHeaderErrorsAndTail()
    {
        var noise = Enumerable.Range(0, 400).Select(i => $"Restoring package {i} ...............");
        var output = "Command: dotnet build\nExit code: 1\n" +
                     String.Join("\n", noise.Take(200)) +
                     "\nProgram.cs(10,5): error CS1002: ; expected\n" +
                     String.Join("\n", noise.Skip(200)) +
                     "\nBuild FAILED.";

        var condensed = ToolOutputCondenser.Condense(output, 3000);

        Assert.That(condensed.Length, Is.LessThanOrEqualTo(3000));
        Assert.That(condensed, Does.StartWith("Command: dotnet build\nExit code: 1\n[Output shortened from"));
        Assert.That(condensed, Does.Contain("error CS1002"));
        Assert.That(condensed, Does.EndWith("Build FAILED."));
        Assert.That(condensed, Does.Not.Contain("Restoring package 5 "));
    }
}
