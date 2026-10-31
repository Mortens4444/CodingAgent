using CodingAgent.Enums;
using CodingAgent.Services;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ProjectDetectionTests
{
    private TempWorkspace _workspace = null!;

    [SetUp]
    public void Setup() => _workspace = new TempWorkspace();

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [TestCase("App.csproj", ProgrammingLanguage.CSharp)]
    [TestCase("App.sln", ProgrammingLanguage.CSharp)]
    [TestCase("pyproject.toml", ProgrammingLanguage.Python)]
    [TestCase("script.pyw", ProgrammingLanguage.Python)]
    [TestCase("main.c", ProgrammingLanguage.C)]
    [TestCase("Makefile", ProgrammingLanguage.C)]
    [TestCase("main.cpp", ProgrammingLanguage.Cpp)]
    [TestCase("util.hpp", ProgrammingLanguage.Cpp)]
    [TestCase("pom.xml", ProgrammingLanguage.Java)]
    [TestCase("App.java", ProgrammingLanguage.Java)]
    [TestCase("package.json", ProgrammingLanguage.JavaScript)]
    [TestCase("index.mjs", ProgrammingLanguage.JavaScript)]
    [TestCase("tsconfig.json", ProgrammingLanguage.TypeScript)]
    [TestCase("view.tsx", ProgrammingLanguage.TypeScript)]
    [TestCase("index.htm", ProgrammingLanguage.Html)]
    [TestCase("boot.asm", ProgrammingLanguage.Assembly)]
    [TestCase("start.S", ProgrammingLanguage.Assembly)]
    [TestCase("main.pas", ProgrammingLanguage.Pascal)]
    [TestCase("unit.pp", ProgrammingLanguage.Pascal)]
    [TestCase("main.lisp", ProgrammingLanguage.Lisp)]
    [TestCase("main.cl", ProgrammingLanguage.Lisp)]
    [TestCase("README.md", ProgrammingLanguage.Unknown)]
    public void Classifier_IdentifiesLanguage(string fileName, ProgrammingLanguage expected)
    {
        var (language, _) = LanguageClassifier.IdentifyFromFile(Path.Combine(_workspace.Root, fileName), _workspace.Root);

        Assert.That(language, Is.EqualTo(expected));
    }

    [Test]
    public void Classifier_ReturnsProjectFileOnlyForDotNet()
    {
        var (_, csproj) = LanguageClassifier.IdentifyFromFile(Path.Combine(_workspace.Root, "src", "App.csproj"), _workspace.Root);
        var (_, python) = LanguageClassifier.IdentifyFromFile(Path.Combine(_workspace.Root, "setup.py"), _workspace.Root);

        Assert.That(csproj, Is.EqualTo(Path.Combine("src", "App.csproj")));
        Assert.That(python, Is.Empty);
    }

    [Test]
    public void Scanner_CppWithMakefileAndHeaders_IsCpp()
    {
        _workspace.Write("Makefile");
        _workspace.Write("include/a.h");
        _workspace.Write("include/b.h");
        _workspace.Write("src/main.cpp");

        Assert.That(ProjectDirectoryScanner.DetectProject(_workspace.Root).Language, Is.EqualTo(ProgrammingLanguage.Cpp));
    }

    [Test]
    public void Scanner_OnlyNeutralFiles_FallsBackToThem()
    {
        _workspace.Write("CMakeLists.txt");

        Assert.That(ProjectDirectoryScanner.DetectProject(_workspace.Root).Language, Is.EqualTo(ProgrammingLanguage.C));
    }

    [Test]
    public void Scanner_TypeScriptProjectWithSomeJavaScript_IsTypeScript()
    {
        _workspace.Write("package.json");
        _workspace.Write("tsconfig.json");
        _workspace.Write("src/app.ts");
        _workspace.Write("jest.config.js");

        Assert.That(ProjectDirectoryScanner.DetectProject(_workspace.Root).Language, Is.EqualTo(ProgrammingLanguage.TypeScript));
    }

    [Test]
    public void Scanner_JavaScriptSiteWithHtml_IsJavaScript()
    {
        _workspace.Write("index.html");
        _workspace.Write("package.json");
        _workspace.Write("app.js");

        Assert.That(ProjectDirectoryScanner.DetectProject(_workspace.Root).Language, Is.EqualTo(ProgrammingLanguage.JavaScript));
    }

    [Test]
    public void Scanner_ProjectFileWins_AndExcludedFoldersAreSkipped()
    {
        _workspace.Write("node_modules/x/index.js");
        _workspace.Write("tools/a.py");
        _workspace.Write("tools/b.py");
        _workspace.Write("src/App/App.csproj");

        var (language, projectFile) = ProjectDirectoryScanner.DetectProject(_workspace.Root);

        Assert.That(language, Is.EqualTo(ProgrammingLanguage.CSharp));
        Assert.That(projectFile, Is.EqualTo(Path.Combine("src", "App", "App.csproj")));
    }

    [Test]
    public void Scanner_EmptyFolder_IsUnknown()
    {
        Assert.That(ProjectDirectoryScanner.DetectProject(_workspace.Root).Language, Is.EqualTo(ProgrammingLanguage.Unknown));
    }

    [Test]
    public void Identifier_SourceFile_UsesItsFolder()
    {
        var file = _workspace.Write("main.pas", "program Main;");

        var resolved = new ProjectIdentifier().ResolveProject(file);

        Assert.That(resolved.Root, Is.EqualTo(Path.GetFullPath(_workspace.Root)));
        Assert.That(resolved.ProgrammingLanguage, Is.EqualTo(ProgrammingLanguage.Pascal));
    }

    [Test]
    public void Identifier_UnknownFile_Throws()
    {
        var file = _workspace.Write("notes.md");

        Assert.Throws<InvalidOperationException>(() => new ProjectIdentifier().ResolveProject(file));
    }

    [Test]
    public void Identifier_MissingDirectory_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => new ProjectIdentifier().ResolveProject(Path.Combine(_workspace.Root, "missing")));
    }

    [Test]
    public void Identifier_MultipleSolutions_Throws()
    {
        _workspace.Write("A.sln");
        _workspace.Write("B.slnx");

        var ex = Assert.Throws<InvalidOperationException>(() => new ProjectIdentifier().ResolveProject(_workspace.Root));
        Assert.That(ex!.Message, Does.Contain("A.sln"));
    }

    [Test]
    public void Identifier_Directory_ScansForLanguage()
    {
        _workspace.Write("src/main.lisp");

        var resolved = new ProjectIdentifier().ResolveProject(_workspace.Root);

        Assert.That(resolved.ProgrammingLanguage, Is.EqualTo(ProgrammingLanguage.Lisp));
        Assert.That(resolved.ProjectFile, Is.Empty);
    }
}
