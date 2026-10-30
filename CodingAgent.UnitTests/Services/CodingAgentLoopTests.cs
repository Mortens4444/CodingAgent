using CodingAgent.Services;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Mocklis;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class CodingAgentLoopTests
{
    private MockChatClientForLoop _mockChatClient = null!;
    private MockCodingLanguageServiceForLoop _mockLanguageService = null!;
    private ToolDispatcher _toolDispatcher = null!;
    private CodingAgentLoop _agentLoop = null!;
    private string _tempWorkspace = null!;

    private TextWriter _originalOut = null!;
    private TextWriter _originalError = null!;

    [SetUp]
    public void Setup()
    {
        _mockChatClient = new MockChatClientForLoop();
        _mockLanguageService = new MockCodingLanguageServiceForLoop();

        _tempWorkspace = Path.Combine(Path.GetTempPath(), "AgentLoopTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempWorkspace);

        var inMemorySettings = new Dictionary<string, string?>
            {
                {"AgentWorkspace", _tempWorkspace}
            };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _toolDispatcher = new ToolDispatcher(configuration);
        _agentLoop = new CodingAgentLoop(_mockChatClient, _toolDispatcher);

        _originalOut = Console.Out;
        _originalError = Console.Error;
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);

        ((IDisposable)_mockChatClient).Dispose();

        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task RunAsync_WhenChatClientThrowsException_CatchesAndReturnsGracefully()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Do something") };

        // Use .Func for dynamic delegates / throwing exceptions
        _mockChatClient.GetResponseAsync.Func(_ => throw new InvalidOperationException("Ollama connection failed"));

        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act & Assert
        await Assert.ThatAsync(async () => await _agentLoop.RunAsync(_mockLanguageService, messages), Throws.Nothing);
        Assert.That(sw.ToString(), Does.Contain("Error when calling Ollama: Ollama connection failed"));
    }

    [Test]
    public async Task RunAsync_WhenModelReturnsFinishTool_PrintsSummaryAndTerminates()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Finish the task") };
        string finishResponse = "{\"name\": \"finish\", \"arguments\": {\"summary\": \"All tasks completed successfully.\"}}";

        // Use .Return for fixed task return values
        _mockChatClient.GetResponseAsync.Return(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, finishResponse))));

        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        await _agentLoop.RunAsync(_mockLanguageService, messages);

        // Assert
        string output = sw.ToString();
        Assert.That(output, Does.Contain("=== Agent summary ==="));
        Assert.That(output, Does.Contain("All tasks completed successfully."));
    }

    [Test]
    public async Task RunAsync_WhenModelReturnsInvalidJson_AddsParseErrorToMessagesAndContinues()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Do work") };

        int callCount = 0;
        _mockChatClient.GetResponseAsync.Func(_ =>
        {
            callCount++;
            string responseText = callCount == 1
                ? "Not valid JSON response"
                : "{\"name\": \"finish\", \"arguments\": {\"summary\": \"Done after parse error\"}}";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        });

        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        await _agentLoop.RunAsync(_mockLanguageService, messages);

        // Assert
        Assert.That(messages.Any(m => m.Text != null && m.Text.Contains("HOST ERROR:")), Is.True);
        Assert.That(sw.ToString(), Does.Contain("Done after parse error"));
    }

    [Test]
    public async Task RunAsync_WhenModelReturnsInvalidToolName_AddsToolErrorToMessagesAndContinues()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Do work") };

        int callCount = 0;
        _mockChatClient.GetResponseAsync.Func(_ =>
        {
            callCount++;
            string responseText = callCount == 1
                ? "{\"name\": \"non_existent_tool\"}"
                : "{\"name\": \"finish\", \"arguments\": {\"summary\": \"Done after invalid tool\"}}";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        });

        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        await _agentLoop.RunAsync(_mockLanguageService, messages);

        // Assert
        Assert.That(messages.Any(m => m.Text != null && m.Text.Contains("is not an existing or valid tool name")), Is.True);
        Assert.That(sw.ToString(), Does.Contain("Done after invalid tool"));
    }

    [Test]
    public async Task RunAsync_WhenValidToolCalled_ExecutesToolAndAppendsResult()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Check missing files") };

        int callCount = 0;
        _mockChatClient.GetResponseAsync.Func(_ =>
        {
            callCount++;
            string responseText = callCount == 1
                ? "{\"name\": \"check_for_missing_files\"}"
                : "{\"name\": \"finish\", \"arguments\": {\"summary\": \"Done after tool execution\"}}";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        });

        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        await _agentLoop.RunAsync(_mockLanguageService, messages);

        // Assert
        Assert.That(sw.ToString(), Does.Contain("[Tool result: check_for_missing_files]"));
        Assert.That(sw.ToString(), Does.Contain("Done after tool execution"));
    }
}