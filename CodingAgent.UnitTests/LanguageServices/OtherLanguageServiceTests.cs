using CodingAgent.Services.LanguageServices;
using CodingAgent.UnitTests.Mocks;

namespace CodingAgent.UnitTests.LanguageServices;

[TestFixture]
public class OtherLanguageServiceTests
{
    private TempWorkspace _workspace = null!;
    private RecordingProcessRunner _runner = null!;

    private static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    private static string Null => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _runner = new RecordingProcessRunner();
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [Test]
    public async Task Assembly_Build_AssemblesNasmFilesThenLinks()
    {
        _workspace.Write("main.asm");
        _workspace.Write("lib/io.asm");
        _workspace.Write("start.s");
        var format = OperatingSystem.IsWindows() ? "win64" : "elf64";
        var noPie = OperatingSystem.IsWindows() ? "" : " -no-pie";

        await new AssemblyCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            $"nasm -f {format} lib/io.asm -o build/obj/io.o",
            $"nasm -f {format} main.asm -o build/obj/main.o",
            $"gcc build/obj/io.o build/obj/main.o start.s -o {Exe("program")}{noPie}"
        }));
    }

    [Test]
    public async Task Assembly_Build_StopsOnAssemblerError()
    {
        _workspace.Write("main.asm");
        _runner.Result = (_, _) => RecordingProcessRunner.Failure("error: parser");

        var result = await new AssemblyCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(result, Does.Contain("error: parser"));
        Assert.That(_runner.Commands, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Assembly_Build_WithMakefile_UsesMake()
    {
        _workspace.Write("Makefile");

        await new AssemblyCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "make" }));
    }

    [Test]
    public async Task Assembly_ValidateSyntax_ChecksEveryFile()
    {
        _workspace.Write("main.asm");
        _workspace.Write("start.s");
        var service = new AssemblyCodingLanguageService(_workspace.Root, _runner.Mock);

        var ok = await service.ValidateSyntaxAsync();
        _runner.Result = (_, _) => RecordingProcessRunner.Failure("bad");
        var failed = await service.ValidateSyntaxAsync();

        Assert.That(ok, Does.Contain("Checked 2 assembly files."));
        Assert.That(failed, Does.Contain("bad"));
        Assert.That(_runner.Commands[1], Is.EqualTo($"gcc -c start.s -o {Null}"));
    }

    [Test]
    public async Task Assembly_NoSources_ReportsError()
    {
        var service = new AssemblyCodingLanguageService(_workspace.Root, _runner.Mock);

        Assert.That(await service.BuildAsync(), Does.StartWith("ERROR: No assembly"));
        Assert.That(await service.ValidateSyntaxAsync(), Does.StartWith("ERROR: No assembly"));
        Assert.That(await service.RunTestsAsync(), Does.StartWith("No "));
    }

    [Test]
    public async Task Pascal_BuildsMainProgramAndRunsTestProgram()
    {
        _workspace.Write("utils.pas", "unit Utils;");
        _workspace.Write("app.pas", "program App;\nbegin end.");
        _workspace.Write("tests/test.pas", "program Test;\nbegin end.");
        _runner.Result = (name, _) =>
        {
            if (name == "fpc") _workspace.Write(Exe("tests/test"));
            return RecordingProcessRunner.Success("");
        };
        var service = new PascalCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.BuildAsync();
        await service.ValidateSyntaxAsync();
        await service.RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            "fpc -vewn app.pas",
            "fpc -s -vewn app.pas",
            "fpc -vewn tests/test.pas",
            Exe("test")
        }));
    }

    [Test]
    public async Task Pascal_PrefersLprThenMain()
    {
        _workspace.Write("main.pas", "program Main;");
        _workspace.Write("project.lpr", "program Project;");

        await new PascalCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "fpc -vewn project.lpr" }));
    }

    [Test]
    public async Task Pascal_WithoutTests_RunsBuiltProgramOrExplains()
    {
        _workspace.Write("main.pas", "program Main;");
        var service = new PascalCodingLanguageService(_workspace.Root, _runner.Mock);

        var notBuilt = await service.RunTestsAsync();
        _workspace.Write(Exe("main"));
        await service.RunTestsAsync();

        Assert.That(notBuilt, Does.StartWith("No test program found"));
        Assert.That(_runner.Commands, Is.EqualTo(new[] { Exe("main") }));
    }

    [Test]
    public async Task Pascal_NoSources_ReportsError()
    {
        var service = new PascalCodingLanguageService(_workspace.Root, _runner.Mock);

        Assert.That(await service.BuildAsync(), Does.StartWith("ERROR: No Pascal program"));
        Assert.That(await service.RunTestsAsync(), Does.StartWith("ERROR: No Pascal program"));
        Assert.That(await service.ValidateSyntaxAsync(), Does.StartWith("ERROR: No Pascal program"));
    }

    [Test]
    public async Task Lisp_BuildLoadsMainAndTestsRunAsScripts()
    {
        _workspace.Write("main.lisp");
        _workspace.Write("util.lisp");
        _workspace.Write("tests/a.lisp");
        _workspace.Write("tests/b.lisp");
        var service = new LispCodingLanguageService(_workspace.Root, _runner.Mock);

        var build = await service.BuildAsync();
        await service.RunTestsAsync();

        Assert.That(build, Does.StartWith("Common Lisp has no separate build step"));
        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            "sbcl --noinform --non-interactive --no-userinit --load main.lisp",
            "sbcl --script tests/a.lisp",
            "sbcl --script tests/b.lisp"
        }));
    }

    [Test]
    public async Task Lisp_WithoutMain_LoadsEverySourceAndStopsTestsOnFailure()
    {
        _workspace.Write("a.lisp");
        _workspace.Write("b.cl");
        _workspace.Write("test.lisp");
        _workspace.Write("tests.lisp");
        _runner.Result = (_, args) => args[0] == "--script" ? RecordingProcessRunner.Failure("fail") : RecordingProcessRunner.Success("");
        var service = new LispCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.ValidateSyntaxAsync();
        var tests = await service.RunTestsAsync();

        Assert.That(tests, Does.Contain("fail"));
        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            "sbcl --noinform --non-interactive --no-userinit --load a.lisp --load b.cl",
            "sbcl --script test.lisp"
        }));
    }

    [Test]
    public async Task Lisp_AsdfSystem_LoadsAndTestsTheShortestSystem()
    {
        _workspace.Write("app.asd");
        _workspace.Write("app-test.asd");
        _workspace.Write("src/main.lisp");
        var service = new LispCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.ValidateSyntaxAsync();
        await service.RunTestsAsync();

        Assert.That(_runner.Commands, Has.Count.EqualTo(2));
        Assert.That(_runner.Commands[0], Does.Contain("(asdf:load-asd (merge-pathnames \"app.asd\" (uiop:getcwd)))"));
        Assert.That(_runner.Commands[0], Does.Contain("quicklisp/setup.lisp").And.Contain(":quickload \"app\""));
        Assert.That(_runner.Commands[0], Does.Not.Contain("test-system"));
        Assert.That(_runner.Commands[1], Does.EndWith("--eval (asdf:test-system \"app\")"));
    }

    [Test]
    public async Task Lisp_NoSources_ReportsError()
    {
        var service = new LispCodingLanguageService(_workspace.Root, _runner.Mock);

        Assert.That(await service.ValidateSyntaxAsync(), Does.StartWith("ERROR: No Lisp"));
        Assert.That(await service.RunTestsAsync(), Does.StartWith("No Lisp tests found"));
    }

    [Test]
    public async Task Python_RunTests_FallsBackToUnittestWithoutPytest()
    {
        _runner.Result = (_, args) => args.Contains("pytest") ? RecordingProcessRunner.Failure("No module named pytest") : RecordingProcessRunner.Success("OK");

        var result = await new PythonCodingLanguagetService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(result, Does.StartWith("pytest is not installed"));
        Assert.That(_runner.Commands, Is.EqualTo(new[] { "python -m pytest -q", "python -m unittest discover -v" }));
    }

    [Test]
    public async Task Python_Build_ValidatesSyntax()
    {
        var result = await new PythonCodingLanguagetService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(result, Does.StartWith("Python AST syntax validation"));
        Assert.That(_runner.Commands.Single(), Does.StartWith("python -c"));
    }

    [Test]
    public async Task Python_FallsBackToPyLauncherThenReportsMissing()
    {
        _runner.Missing.Add("python");
        var service = new PythonCodingLanguagetService(_workspace.Root, _runner.Mock);

        await service.RunTestsAsync();
        _runner.Missing.UnionWith(["py", "python3"]);
        var missing = await service.ValidateSyntaxAsync();

        Assert.That(_runner.Commands[0], Is.EqualTo("py -3 -m pytest -q"));
        Assert.That(missing, Does.StartWith("ERROR: 'python' was not found on PATH"));
    }
}
