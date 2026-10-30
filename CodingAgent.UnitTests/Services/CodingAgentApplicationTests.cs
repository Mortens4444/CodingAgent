using CodingAgent.Services;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Mocklis;
using Mocklis.Steps;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class CodingAgentApplicationTests
{
    private MockToDoProvider _mockToDoProvider = null!;
    private MockChatClientForApp _mockChatClient = null!;
    private ToolDispatcher _toolDispatcher = null!;
    private ProjectContextLoader _projectContextLoader = null!;
    private CodingAgentLoop _agentLoop = null!;
    private CodingAgentApplication _application = null!;

    private string _tempWorkspace = null!;
    private TextReader _originalIn = null!;
    private TextWriter _originalOut = null!;
    private TextWriter _originalError = null!;

    [SetUp]
    public void Setup()
    {
        _mockToDoProvider = new MockToDoProvider();
        _mockChatClient = new MockChatClientForApp();

        _tempWorkspace = Path.Combine(Path.GetTempPath(), "AgentAppTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempWorkspace);

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"AgentWorkspace", _tempWorkspace}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _toolDispatcher = new ToolDispatcher(configuration);
        _projectContextLoader = new ProjectContextLoader(_mockToDoProvider);
        _agentLoop = new CodingAgentLoop(_mockChatClient, _toolDispatcher);

        _application = new CodingAgentApplication(_projectContextLoader, _agentLoop);

        _originalIn = Console.In;
        _originalOut = Console.Out;
        _originalError = Console.Error;
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetIn(_originalIn);
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);

        ((IDisposable)_mockChatClient).Dispose();

        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task RunAsync_WhenProjectContextIsNull_DoesNotRunAgentLoop()
    {
        // Arrange - Provide empty input so ProjectContextLoader returns null early
        Console.SetIn(new StringReader("\n"));
        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Configure mock chat client to throw if invoked, verifying it's never called
        _mockChatClient.GetResponseAsync.Func(_ => throw new InvalidOperationException("ChatClient should not be called"));

        // Act
        await _application.RunAsync();

        // Assert
        Assert.That(sw.ToString(), Does.Contain("You did not provide a path."));
    }

    [Test]
    public async Task RunAsync_WhenProjectContextIsValid_RunsAgentLoopSuccessfully()
    {
        // Arrange - Create a solution file so project resolution succeeds
        File.WriteAllText(Path.Combine(_tempWorkspace, "TestApp.sln"), "");
        Console.SetIn(new StringReader(_tempWorkspace + "\n"));

        string todoPath = Path.Combine(_tempWorkspace, "ToDo.txt");
        _mockToDoProvider.GetToDoAsync.Return(Task.FromResult<string?>("Initial task"));

        string finishResponse = "{\"name\": \"finish\", \"arguments\": {\"summary\": \"Completed successfully\"}}";
        _mockChatClient.GetResponseAsync.Return(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, finishResponse))));

        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        await _application.RunAsync();

        // Assert
        string output = sw.ToString();
        Assert.That(output, Does.Contain("Project root:"));
        Assert.That(output, Does.Contain("=== Agent summary ==="));
        Assert.That(output, Does.Contain("Completed successfully"));
    }
}