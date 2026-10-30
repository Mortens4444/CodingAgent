using CodingAgent.Interfaces;
using System.ComponentModel;

namespace CodingAgent.Services.LanguageServices;

public class PythonCodingLanguagetService(string root) : LanguageService(root), ICodingLanguageService
{
    public async Task<string> BuildAsync()
    {
        var result = await ValidateSyntaxAsync();
        return "Python AST syntax validation (no bytecode files are written)\n" + result;
    }

    public async Task<string> RunTestsAsync()
    {
        var pytestResult = await RunPythonAsync(["-m", "pytest", "-q"]);
        if (pytestResult.Contains("No module named pytest", StringComparison.OrdinalIgnoreCase) ||
            pytestResult.Contains("No module named 'pytest'", StringComparison.OrdinalIgnoreCase))
        {
            return "pytest is not installed; falling back to standard-library unittest discovery.\n" +
                   await RunPythonAsync(["-m", "unittest", "discover", "-v"]);
        }

        return pytestResult;
    }

    public Task<string> ValidateSyntaxAsync()
    {
        const string script = """
import ast
from pathlib import Path
import sys

excluded = {
    ".git", ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache",
    ".ruff_cache", ".tox", "node_modules", "build", "dist", ".codingagent-backups"
}
files = sorted(
    path for path in Path(".").rglob("*")
    if path.is_file()
    and path.suffix.lower() in {".py", ".pyw"}
    and not any(part in excluded for part in path.parts)
)
errors = []
for path in files:
    try:
        source = path.read_text(encoding="utf-8-sig")
        ast.parse(source, filename=str(path))
    except Exception as exc:
        errors.append(f"{path}: {type(exc).__name__}: {exc}")
if errors:
    print("Python syntax errors:")
    print("\n".join(errors))
    sys.exit(1)
print(f"Syntax OK. Checked {len(files)} Python files.")
""";

        return RunPythonAsync(["-c", script]);
    }

    private async Task<string> RunPythonAsync(IReadOnlyList<string> args)
    {
        try
        {
            return await RunProcessAsync("python", Root, args, "python " + String.Join(" ", args));
        }
        catch (Win32Exception)
        {
            var pyArgs = new List<string> { "-3" };
            pyArgs.AddRange(args);
            return await RunProcessAsync("py", Root, pyArgs, "py " + String.Join(" ", pyArgs));
        }
    }
}
