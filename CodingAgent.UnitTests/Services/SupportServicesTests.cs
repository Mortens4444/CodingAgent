using CodingAgent.Enums;
using CodingAgent.Extensions;
using CodingAgent.Services.ChatClient;
using CodingAgent.Services.ToDo;
using CodingAgent.UnitTests.Mocks;
using Mocklis;
using OllamaSharp.Models;
using System.Runtime.CompilerServices;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class SupportServicesTests
{
    private TextReader _originalIn = null!;
    private TextWriter _originalOut = null!;
    private TextWriter _originalError = null!;
    private StringWriter _out = null!;

    [SetUp]
    public void Setup()
    {
        _originalIn = Console.In;
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _out = new StringWriter();
        Console.SetOut(_out);
        Console.SetError(_out);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetIn(_originalIn);
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _out.Dispose();
    }

    private enum Sample
    {
        [System.ComponentModel.Description("described")]
        WithDescription,
        WithoutDescription
    }

    [Test]
    public void GetDescription_UsesAttributeOrName()
    {
        Assert.That(Sample.WithDescription.GetDescription(), Is.EqualTo("described"));
        Assert.That(Sample.WithoutDescription.GetDescription(), Is.EqualTo("WithoutDescription"));
        Assert.That(((Sample)42).GetDescription(), Is.Empty);
        Assert.That(ChatClientType.Qwen2_5_coder_14b.GetDescription(), Is.EqualTo("qwen2.5-coder:14b"));
    }

    [Test]
    public async Task ToDoFileReader_ReadsFileOrReportsProblem()
    {
        using var workspace = new TempWorkspace();
        var reader = new ToDoFileReader();
        var todo = workspace.Write("ToDo.txt", "do it");
        var empty = workspace.Write("Empty.txt", "  ");

        Assert.That(await reader.GetToDoAsync(todo), Is.EqualTo("do it"));
        Assert.That(await reader.GetToDoAsync(empty), Is.Null);
        Assert.That(await reader.GetToDoAsync(Path.Combine(workspace.Root, "missing.txt")), Is.Null);
        Assert.That(_out.ToString(), Does.Contain("ToDo.txt is empty."));
        Assert.That(_out.ToString(), Does.Contain("Can't find file ToDo.txt"));
    }

    [Test]
    public async Task AskUserForTask_ReadsTaskFromConsole()
    {
        Console.SetIn(new StringReader("write tests\n"));

        var result = await new AskUserForTask().GetToDoAsync();

        Assert.That(result, Is.EqualTo("write tests"));
        Assert.That(_out.ToString(), Does.Contain("How can I help you?"));
    }

    [Test]
    public void ChatClientFactory_Get_ConfiguresUriModelAndTimeout()
    {
        using var client = ChatClientFactory.Get("http://localhost:11434", ChatClientType.Qwen2_5_coder_7b, 0);

        Assert.That(client.SelectedModel, Is.EqualTo("qwen2.5-coder:7b"));
        Assert.That(client.Uri, Is.EqualTo(new Uri("http://localhost:11434")));
    }

    private static async IAsyncEnumerable<PullModelResponse> Progress(Action onPulled, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new PullModelResponse { Status = "pulling", Total = 100, Completed = 50 };
        yield return new PullModelResponse { Status = "pulling", Total = 100, Completed = 50 };
        yield return new PullModelResponse { Status = "success" };
        onPulled();
    }

    [Test]
    public async Task EnsureModel_AlreadyInstalled_DoesNotPull()
    {
        var client = new MockOllamaApiClient();
        client.ListLocalModelsAsync.Return(Task.FromResult<IEnumerable<Model>>([new Model { Name = "qwen2.5-coder" }]));
        client.PullModelAsync.Func(_ => throw new AssertionException("must not pull"));

        Assert.That(await OllamaModelProvisioner.EnsureModelAsync(client, "qwen2.5-coder:latest"), Is.True);
    }

    [Test]
    public async Task EnsureModel_Missing_PullsAndRechecks()
    {
        var installed = new List<Model>();
        var client = new MockOllamaApiClient();
        client.ListLocalModelsAsync.Func(_ => Task.FromResult<IEnumerable<Model>>(installed.ToList()));
        client.PullModelAsync.Func(_ => Progress(() => installed.Add(new Model { Name = "qwen2.5-coder:14b" })));

        var result = await OllamaModelProvisioner.EnsureModelAsync(client, "qwen2.5-coder:14b");

        Assert.That(result, Is.True);
        Assert.That(_out.ToString(), Does.Contain("Pulling it"));
        Assert.That(_out.ToString(), Does.Contain("pulling 50%"));
        Assert.That(_out.ToString(), Does.Contain("success"));
    }

    [Test]
    public async Task EnsureModel_StillMissingAfterPull_ReturnsFalse()
    {
        var client = new MockOllamaApiClient();
        client.ListLocalModelsAsync.Return(Task.FromResult<IEnumerable<Model>>([]));
        client.PullModelAsync.Func(_ => Progress(() => { }));

        Assert.That(await OllamaModelProvisioner.EnsureModelAsync(client, "x:1b"), Is.False);
        Assert.That(_out.ToString(), Does.Contain("still not available"));
    }

    [Test]
    public async Task EnsureModel_ServerUnreachable_ReturnsFalse()
    {
        var client = new MockOllamaApiClient();
        client.ListLocalModelsAsync.Func(_ => throw new HttpRequestException("connection refused"));

        Assert.That(await OllamaModelProvisioner.EnsureModelAsync(client, "x:1b"), Is.False);
        Assert.That(_out.ToString(), Does.Contain("connection refused"));
    }
}
