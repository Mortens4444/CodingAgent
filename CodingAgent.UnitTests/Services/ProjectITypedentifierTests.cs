using CodingAgent.Enums;
using CodingAgent.Services;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ProjectITypedentifierTests
{
    private string _testWorkspace = null!;
    private ProjectITypedentifier _identifier = null!;

    [SetUp]
    public void Setup()
    {
        _testWorkspace = Path.Combine(Path.GetTempPath(), "ProjectIdentifierTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testWorkspace);
        _identifier = new ProjectITypedentifier();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testWorkspace))
        {
            try { Directory.Delete(_testWorkspace, true); } catch { }
        }
    }

    [Test]
    public void ResolveProject_WhenPathDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        string nonExistentPath = Path.Combine(_testWorkspace, "does_not_exist");

        // Act & Assert
        var ex = Assert.Throws<DirectoryNotFoundException>(() => _identifier.ResolveProject(nonExistentPath));
        Assert.That(ex.Message, Does.StartWith("The specified route cannot be found:"));
    }

    [TestCase(".sln")]
    [TestCase(".slnx")]
    [TestCase(".csproj")]
    public void ResolveProject_WithCSharpProjectOrSolutionFile_ReturnsCSharp(string extension)
    {
        // Arrange
        string fileName = $"MyProject{extension}";
        string filePath = Path.Combine(_testWorkspace, fileName);
        File.WriteAllText(filePath, "// dummy content");

        // Act
        var (root, projectFile, lang) = _identifier.ResolveProject(filePath);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(root, Is.EqualTo(_testWorkspace));
            Assert.That(projectFile, Is.EqualTo(fileName));
            Assert.That(lang, Is.EqualTo(ProgrammingLanguage.CSharp));
        }
    }

    [TestCase("pyproject.toml")]
    [TestCase("requirements.txt")]
    [TestCase("setup.py")]
    [TestCase("setup.cfg")]
    [TestCase("Pipfile")]
    [TestCase("script.py")]
    [TestCase("app.pyw")]
    public void ResolveProject_WithPythonProjectFile_ReturnsPython(string fileName)
    {
        // Arrange
        string filePath = Path.Combine(_testWorkspace, fileName);
        File.WriteAllText(filePath, "# python file");

        // Act
        var (root, projectFile, lang) = _identifier.ResolveProject(filePath);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(root, Is.EqualTo(_testWorkspace));
            Assert.That(projectFile, Is.EqualTo(String.Empty));
            Assert.That(lang, Is.EqualTo(ProgrammingLanguage.Python));
        }
    }

    [Test]
    public void ResolveProject_WithUnsupportedFile_ThrowsInvalidOperationException()
    {
        // Arrange
        string filePath = Path.Combine(_testWorkspace, "readme.txt");
        File.WriteAllText(filePath, "hello");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _identifier.ResolveProject(filePath));
    }

    [Test]
    public void ResolveProject_DirectoryWithMultipleSolutions_ThrowsInvalidOperationException()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testWorkspace, "proj1.sln"), "");
        File.WriteAllText(Path.Combine(_testWorkspace, "proj2.slnx"), "");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _identifier.ResolveProject(_testWorkspace));
        Assert.That(ex.Message, Does.Contain("Multiple solution files found"));
    }

    [Test]
    public void ResolveProject_DirectoryWithSingleSolution_ReturnsCSharpAndSolutionName()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testWorkspace, "MySolution.sln"), "");

        // Act
        var (root, projectFile, lang) = _identifier.ResolveProject(_testWorkspace);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(root, Is.EqualTo(_testWorkspace));
            Assert.That(projectFile, Is.EqualTo("MySolution.sln"));
            Assert.That(lang, Is.EqualTo(ProgrammingLanguage.CSharp));
        }
    }

    [Test]
    public void ResolveProject_DirectoryWithNestedCsproj_DetectsCSharpProject()
    {
        // Arrange
        string subDir = Path.Combine(_testWorkspace, "src", "SubProject");
        Directory.CreateDirectory(subDir);
        string csprojPath = Path.Combine(subDir, "SubProject.csproj");
        File.WriteAllText(csprojPath, "<Project />");

        // Act
        var (root, projectFile, lang) = _identifier.ResolveProject(_testWorkspace);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(root, Is.EqualTo(_testWorkspace));
            Assert.That(projectFile, Is.EqualTo(Path.Combine("src", "SubProject", "SubProject.csproj")));
            Assert.That(lang, Is.EqualTo(ProgrammingLanguage.CSharp));
        }
    }

    [Test]
    public void ResolveProject_DirectoryWithPythonFile_DetectsPythonProject()
    {
        // Arrange
        string subDir = Path.Combine(_testWorkspace, "src", "python_app");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "main.py"), "print('hello')");

        // Act
        var (root, projectFile, lang) = _identifier.ResolveProject(_testWorkspace);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(root, Is.EqualTo(_testWorkspace));
            Assert.That(projectFile, Is.EqualTo(String.Empty));
            Assert.That(lang, Is.EqualTo(ProgrammingLanguage.Python));
        }
    }

    [Test]
    public void ResolveProject_DirectoryExcludesIgnoredFolders()
    {
        // Arrange - Put a python file inside an excluded directory like 'node_modules'
        string ignoredDir = Path.Combine(_testWorkspace, "node_modules", "some-pkg");
        Directory.CreateDirectory(ignoredDir);
        File.WriteAllText(Path.Combine(ignoredDir, "index.py"), "print('ignored')");

        // Act - Root should remain Unknown because the python file is inside an excluded directory
        var (_, __, lang) = _identifier.ResolveProject(_testWorkspace);

        // Assert
        Assert.That(lang, Is.EqualTo(ProgrammingLanguage.Unknown));
    }
}