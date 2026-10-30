using CodingAgent.Enums;
using CodingAgent.Services;
using CodingAgent.UnitTests.Mocks;
using Mocklis;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ProjectContextLoaderTests
{
    private MockToDoProvider _mockToDoProvider = null!;
    private ProjectContextLoader _loader = null!;
    private string _tempWorkspace = null!;

    private TextReader _originalIn = null!;
    private TextWriter _originalOut = null!;
    private TextWriter _originalError = null!;

    [SetUp]
    public void Setup()
    {
        _mockToDoProvider = new MockToDoProvider();
        _loader = new ProjectContextLoader(_mockToDoProvider);

        // Create a real temporary workspace for success scenarios
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ContextLoaderTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempWorkspace);

        // Save original console streams
        _originalIn = Console.In;
        _originalOut = Console.Out;
        _originalError = Console.Error;
    }

    [TearDown]
    public void TearDown()
    {
        // Restore original console streams
        Console.SetIn(_originalIn);
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);

        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task LoadAsync_WhenInputIsEmpty_ReturnsNullAndPrintsWarning()
    {
        // Arrange
        Console.SetIn(new StringReader("   \n"));
        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        var result = await _loader.LoadAsync();

        // Assert
        Assert.That(result, Is.Null);
        Assert.That(sw.ToString(), Does.Contain("You did not provide a path."));
    }

    [Test]
    public async Task LoadAsync_WhenPathResolutionFails_ReturnsNullAndPrintsError()
    {
        // Arrange - Provide a path that doesn't exist
        string nonExistentPath = Path.Combine(_tempWorkspace, "non_existent_folder");
        Console.SetIn(new StringReader(nonExistentPath + "\n"));

        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        // Act
        var result = await _loader.LoadAsync();

        // Assert
        Assert.That(result, Is.Null);
        Assert.That(errorWriter.ToString(), Does.Contain("Error detecting project:"));
    }

    [Test]
    public async Task LoadAsync_WhenValidProjectProvided_ReturnsProjectContextAndFetchesToDo()
    {
        // Arrange - Create a dummy solution file in the temp workspace
        File.WriteAllText(Path.Combine(_tempWorkspace, "TestSolution.sln"), "");

        Console.SetIn(new StringReader(_tempWorkspace + "\n"));
        using var sw = new StringWriter();
        Console.SetOut(sw);

        string expectedTodoText = "Fix unit tests";

        // Configure Mocklis with a direct Task result
        _mockToDoProvider.GetToDoAsync.Return(Task.FromResult<string?>(expectedTodoText));

        // Act
        var result = await _loader.LoadAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Root, Is.EqualTo(_tempWorkspace));
        Assert.That(result.ProjectFile, Is.EqualTo("TestSolution.sln"));
        Assert.That(result.ProgrammingLanguage, Is.EqualTo(ProgrammingLanguage.CSharp));
        Assert.That(result.TodoText, Is.EqualTo(expectedTodoText));
    }

    [Test]
    public async Task LoadAsync_WhenToDoFileMissing_UsesDefaultFallbackText()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempWorkspace, "TestSolution.sln"), "");
        Console.SetIn(new StringReader(_tempWorkspace + "\n"));
        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Configure Mocklis to return a null task directly
        _mockToDoProvider.GetToDoAsync.Return(Task.FromResult<string?>(null));

        // Act
        var result = await _loader.LoadAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TodoText, Is.EqualTo("What do you suggest to do?"));
    }
}