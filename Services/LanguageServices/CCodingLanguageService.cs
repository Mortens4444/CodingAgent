using CodingAgent.Interfaces;
using System.Text.RegularExpressions;

namespace CodingAgent.Services.LanguageServices;

// Shared by C and C++. Build order: CMakeLists.txt, Makefile, then a direct compiler call over all sources.
// Without CMake/make, tests are the sources under test/ or tests/ (or named test_*, *_test) compiled together with
// the project sources that do not define main().
public abstract partial class NativeCodingLanguageService(string root, IProcessRunner? processRunner, string compiler, string[] extensions, string[] flags)
    : LanguageService(root, processRunner), ICodingLanguageService
{
    private static readonly string Program = ExecutableName("program");
    private static readonly string TestProgram = ExecutableName("test_program");

    public async Task<string> BuildAsync()
    {
        if (HasFile("CMakeLists.txt"))
        {
            var configure = await RunProcessAsync("cmake", ["-B", "build", "-S", "."]);
            return Succeeded(configure) ? await RunProcessAsync("cmake", ["--build", "build"]) : configure;
        }

        if (HasMakefile)
            return await RunProcessAsync(MakeCommand, []);

        var sources = FindSourceFiles(extensions).Where(file => !IsTestFile(file)).ToList();
        if (sources.Count == 0)
            return NoSourcesMessage(compiler);

        return await RunProcessAsync(compiler, [.. flags, .. sources, "-o", Program]);
    }

    public async Task<string> RunTestsAsync()
    {
        if (HasFile("CMakeLists.txt"))
            return await RunProcessAsync("ctest", ["--test-dir", "build", "--output-on-failure"]);

        if (HasMakefile)
            return await RunProcessAsync(MakeCommand, ["test"]);

        var allSources = FindSourceFiles(extensions);
        var tests = allSources.Where(IsTestFile).ToList();
        if (tests.Count == 0)
            return await RunExecutableAsync(Program,
                $"No test sources (test/ or tests/ folder) and no built {Program}. Run build_project first or add tests.");

        var libraries = allSources.Where(file => !IsTestFile(file) && !DefinesMain(file));
        var compile = await RunProcessAsync(compiler, [.. flags, .. tests, .. libraries, "-o", TestProgram]);
        if (!Succeeded(compile))
            return compile;

        return await RunExecutableAsync(TestProgram, $"ERROR: {TestProgram} was not produced.");
    }

    public async Task<string> ValidateSyntaxAsync()
    {
        var sources = FindSourceFiles(extensions);
        return sources.Count == 0
            ? NoSourcesMessage(compiler)
            : await RunProcessAsync(compiler, ["-fsyntax-only", .. flags, .. sources]);
    }

    private bool HasMakefile => HasFile("Makefile", "makefile", "GNUmakefile");

    // WinLibs/MinGW ship make as mingw32-make.
    private string MakeCommand =>
        ProcessRunner.FindExecutable("make") is null && ProcessRunner.FindExecutable("mingw32-make") is not null ? "mingw32-make" : "make";

    private bool DefinesMain(string relativePath)
    {
        try
        {
            return MainRegex().IsMatch(File.ReadAllText(Path.Combine(Root, relativePath)));
        }
        catch (IOException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"\bmain\s*\(")]
    private static partial Regex MainRegex();
}

public class CCodingLanguageService(string root, IProcessRunner? processRunner = null)
    : NativeCodingLanguageService(root, processRunner, "gcc", [".c"], ["-Wall", "-Wextra"]);

public class CppCodingLanguageService(string root, IProcessRunner? processRunner = null)
    : NativeCodingLanguageService(root, processRunner, "g++", [".cpp", ".cc", ".cxx"], ["-std=c++17", "-Wall", "-Wextra"]);
