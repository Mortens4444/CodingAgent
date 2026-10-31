using CodingAgent.Services.LanguageServices;
using CodingAgent.UnitTests.Mocks;

namespace CodingAgent.UnitTests.LanguageServices;

[TestFixture]
public class NativeCodingLanguageServiceTests
{
    private TempWorkspace _workspace = null!;
    private RecordingProcessRunner _runner = null!;

    private static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _runner = new RecordingProcessRunner();
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [Test]
    public async Task Build_WithoutBuildFiles_CompilesExpandedSourcesExceptTests()
    {
        _workspace.Write("main.c", "int main(void) { return 0; }");
        _workspace.Write("src/util.c");
        _workspace.Write("tests/test_util.c");
        _workspace.Write("build/generated.c");

        await new CCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { $"gcc -Wall -Wextra main.c src/util.c -o {Exe("program")}" }));
    }

    [Test]
    public async Task Build_WithCMake_ConfiguresThenBuilds()
    {
        _workspace.Write("CMakeLists.txt");

        await new CppCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "cmake -B build -S .", "cmake --build build" }));
    }

    [Test]
    public async Task Build_WhenCMakeConfigureFails_StopsAndReturnsError()
    {
        _workspace.Write("CMakeLists.txt");
        _runner.Result = (_, _) => RecordingProcessRunner.Failure("CMake Error");

        var result = await new CCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(result, Does.Contain("CMake Error"));
        Assert.That(_runner.Commands, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Build_WithMakefile_UsesMingwMakeWhenOnlyThatExists()
    {
        _workspace.Write("Makefile");
        _runner.Missing.Add("make");

        await new CCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "mingw32-make" }));
    }

    [Test]
    public async Task Build_WithoutSources_ReportsError()
    {
        var result = await new CppCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(result, Does.StartWith("ERROR: No g++ source files"));
        Assert.That(_runner.Commands, Is.Empty);
    }

    [Test]
    public async Task Build_WhenCompilerMissing_ReturnsInstallHint()
    {
        _workspace.Write("main.cpp");
        _runner.Missing.Add("g++");

        var result = await new CppCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(result, Does.StartWith("ERROR: 'g++' was not found on PATH"));
        Assert.That(result, Does.Contain("Install:"));
    }

    [Test]
    public async Task RunTests_WithTestSources_CompilesTestsWithLibrariesWithoutMainAndRunsThem()
    {
        _workspace.Write("main.c", "int main(void) { return 0; }");
        _workspace.Write("calc.c", "int add(int a, int b) { return a + b; }");
        _workspace.Write("tests/test_calc.c", "int main(void) { return 0; }");
        _runner.Result = (name, _) =>
        {
            if (name == "gcc") _workspace.Write(Exe("test_program"));
            return RecordingProcessRunner.Success("ok");
        };

        await new CCodingLanguageService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            $"gcc -Wall -Wextra tests/test_calc.c calc.c -o {Exe("test_program")}",
            Exe("test_program")
        }));
    }

    [Test]
    public async Task RunTests_WhenTestCompileFails_ReturnsCompilerOutput()
    {
        _workspace.Write("tests/test_calc.c");
        _runner.Result = (_, _) => RecordingProcessRunner.Failure("undefined reference");

        var result = await new CCodingLanguageService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(result, Does.Contain("undefined reference"));
        Assert.That(_runner.Commands, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RunTests_WithoutTestsOrProgram_ExplainsWhatIsMissing()
    {
        _workspace.Write("main.c");

        var result = await new CCodingLanguageService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(result, Does.StartWith("No test sources"));
    }

    [Test]
    public async Task RunTests_WithoutTestsButBuiltProgram_RunsProgram()
    {
        _workspace.Write("main.c");
        _workspace.Write(Exe("program"));

        await new CCodingLanguageService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { Exe("program") }));
    }

    [TestCase("CMakeLists.txt", "ctest --test-dir build --output-on-failure")]
    [TestCase("Makefile", "make test")]
    public async Task RunTests_WithBuildSystem_DelegatesToIt(string buildFile, string expected)
    {
        _workspace.Write(buildFile);

        await new CppCodingLanguageService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public async Task ValidateSyntax_ChecksAllSources()
    {
        _workspace.Write("a.cpp");
        _workspace.Write("lib/b.cc");

        await new CppCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "g++ -fsyntax-only -std=c++17 -Wall -Wextra a.cpp lib/b.cc" }));
    }

    [Test]
    public async Task ValidateSyntax_WithoutSources_ReportsError()
    {
        var result = await new CCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(result, Does.StartWith("ERROR: No gcc source files"));
    }
}
