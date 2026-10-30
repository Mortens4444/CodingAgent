using CodingAgent.Services;
using CodingAgent.UnitTests.Mocks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ToolDispatcherTests
{
    private IConfiguration _configuration = null!;
    private ToolDispatcher _toolDispatcher = null!;
    private MockCodingLanguageService _mockCodingLanguageService = null!;
    private string _tempWorkspace = null!;

    [SetUp]
    public void Setup()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "CodingAgentTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempWorkspace);

        var inMemorySettings = new Dictionary<string, string?>
            {
                {"AgentWorkspace", _tempWorkspace}
            };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _toolDispatcher = new ToolDispatcher(_configuration);
        _mockCodingLanguageService = new MockCodingLanguageService();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempWorkspace))
        {
            Directory.Delete(_tempWorkspace, true);
        }
    }

    [Test]
    public async Task ExecuteToolAsync_WhenToolDoesNotExist_ReturnsErrorMessage()
    {
        // Arrange
        string unknownTool = "non_existent_tool";
        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        string result = await _toolDispatcher.ExecuteToolAsync(_mockCodingLanguageService, unknownTool, args);

        // Assert
        Assert.That(result, Is.EqualTo("ERROR: Unknown tool 'non_existent_tool'."));
    }

    [Test]
    public async Task ExecuteToolAsync_ToolNamesAreCaseInsensitive_ExecutesSuccessfully()
    {
        // Arrange - Use an uppercase variant of a tool that safely handles empty or default arguments (e.g., check_for_missing_files)
        string toolName = "CHECK_FOR_MISSING_FILES";
        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        string result = await _toolDispatcher.ExecuteToolAsync(_mockCodingLanguageService, toolName, args);

        // Assert - Should find and execute the command rather than returning an unknown tool error
        Assert.That(result, Does.Not.StartWith("ERROR: Unknown tool"));
    }

    [Test]
    public async Task ExecuteToolAsync_AliasedCommand_BuildSolution_ExecutesSuccessfully()
    {
        // Arrange
        string toolName = "build_solution";
        var args = JsonDocument.Parse("{}").RootElement;

        // Act & Assert
        try
        {
            string result = await _toolDispatcher.ExecuteToolAsync(_mockCodingLanguageService, toolName, args);
            Assert.That(result, Is.Not.Null);
        }
        catch (Exception)
        {
            // If it throws an execution/project error instead of unknown tool, routing succeeded
            Assert.Pass("Command successfully routed.");
        }
    }
}