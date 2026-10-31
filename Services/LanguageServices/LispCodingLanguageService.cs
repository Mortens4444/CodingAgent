using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

// Common Lisp through SBCL. With an ASDF system definition (<name>.asd in the root) the system is loaded and
// tested with asdf:load-system / asdf:test-system, using Quicklisp for dependencies when it is installed.
// Otherwise validation loads main.lisp (or every non-test source) and tests run test.lisp/tests.lisp or the
// files under test/ or tests/ as scripts.
public class LispCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private static readonly string[] Extensions = [".lisp", ".lsp", ".cl"];

    // --no-userinit skips ~/.sbclrc, which is where Quicklisp is usually loaded, so it is loaded here when present:
    // from the QUICKLISP_HOME environment variable (an install outside the home folder), else ~/quicklisp.
    private const string LoadQuicklisp =
        "(let* ((home (sb-ext:posix-getenv \"QUICKLISP_HOME\")) " +
        "(setup (if (and home (plusp (length home))) " +
        "(merge-pathnames \"setup.lisp\" (sb-ext:parse-native-namestring home nil *default-pathname-defaults* :as-directory t)) " +
        "(merge-pathnames \"quicklisp/setup.lisp\" (user-homedir-pathname))))) " +
        "(when (probe-file setup) (load setup)))";

    public async Task<string> BuildAsync() =>
        "Common Lisp has no separate build step; loaded the sources to check them.\n" + await ValidateSyntaxAsync();

    public async Task<string> RunTestsAsync()
    {
        var system = FindSystem();
        if (system is not null)
            return await RunAsdfAsync(system, "test-system");

        var tests = FindSourceFiles(Extensions).Where(IsTestFile).ToList();
        if (tests.Count == 0)
            return "No Lisp tests found. Add test.lisp or files under tests/ that exit with a non-zero code on failure, " +
                   "or define an ASDF system (<name>.asd) whose test-op signals an error when a test fails.";

        var results = new List<string>();
        foreach (var test in tests)
        {
            var result = await RunProcessAsync("sbcl", ["--script", test]);
            results.Add(result);
            if (!Succeeded(result))
                break;
        }

        return String.Join("\n\n", results);
    }

    public async Task<string> ValidateSyntaxAsync()
    {
        var system = FindSystem();
        if (system is not null)
            return await RunAsdfAsync(system, "load-system");

        var sources = FindSourceFiles(Extensions).Where(file => !IsTestFile(file)).ToList();
        if (sources.Count == 0)
            return NoSourcesMessage("Lisp");

        var main = sources.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Equals("main", StringComparison.OrdinalIgnoreCase));
        List<string> arguments = ["--noinform", "--non-interactive", "--no-userinit"];
        foreach (var source in main is null ? sources : [main])
            arguments.AddRange(["--load", source]);

        return await RunProcessAsync("sbcl", arguments);
    }

    // The .asd file in the root; with several, the shortest name, so app.asd wins over app-test.asd.
    private string? FindSystem() =>
        Directory.EnumerateFiles(Root, "*.asd")
            .Select(file => Path.GetFileName(file))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    // A failing test-op or a load error ends the --non-interactive session with a non-zero exit code.
    // Loading goes through ql:quickload when Quicklisp is available, so missing dependencies are downloaded.
    private Task<string> RunAsdfAsync(string asdFile, string operation)
    {
        var system = Path.GetFileNameWithoutExtension(asdFile).ToLowerInvariant();
        List<string> arguments =
        [
            "--noinform", "--non-interactive", "--no-userinit",
            "--eval", LoadQuicklisp,
            "--eval", "(require :asdf)",
            "--eval", $"(asdf:load-asd (merge-pathnames \"{asdFile}\" (uiop:getcwd)))",
            "--eval", $"(if (find-package :ql) (uiop:symbol-call :ql :quickload \"{system}\") (asdf:load-system \"{system}\"))"
        ];

        if (operation == "test-system")
            arguments.AddRange(["--eval", $"(asdf:test-system \"{system}\")"]);

        return RunProcessAsync("sbcl", arguments);
    }
}
