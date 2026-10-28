using CodingAgent.Services;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

internal static class Program
{
    const int maxLength = 120000;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".razor", ".cshtml", ".xaml", ".json", ".xml", ".csproj", ".props",
        ".targets", ".md", ".txt", ".config", ".yaml", ".yml", ".resx", ".sln", ".slnx",
        ".py", ".pyw", ".toml", ".ini", ".cfg", ".ipynb", ".sh", ".ps1", ".bat", ".sql"
    };

    public static async Task Main()
    {
        //var builder = Host.CreateApplicationBuilder(args);

        //// 1. Naplózás hozzáadása (opcionális, de ajánlott)
        //builder.Logging.AddConsole();

        //// 2. Az IChatClient regisztrálása a DI konténerbe Ollama háttérrel
        //builder.Services.AddChatClient(sp =>
        //    new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.1")
        //)
        //.UseLogging(); // Opcionális: beépített AI-hívás naplózás a pipeline-ba

        //// 3. A saját üzleti logikát tartalmazó osztály regisztrálása
        //builder.Services.AddTransient<AiChatService>();

        //var host = builder.Build();

        //// 4. Az alkalmazás indítása a konténerből
        //var chatService = host.Services.GetRequiredService<AiChatService>();
        //await chatService.RunAsync();



        Console.Write("Add meg a projekt könyvtárát vagy a .sln/.slnx/.csproj fájl útvonalát: ");
        var target = Console.ReadLine()?.Trim().Trim('"');

        if (String.IsNullOrWhiteSpace(target))
        {
            Console.WriteLine("Nem adtál meg útvonalat.");
            return;
        }

        string root;
        string projectFile;
        string projectKind;
        try
        {
            (root, projectFile, projectKind) = new ProjectITypedentifier().ResolveProject(target);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error detecting project: {ex.Message}");
            return;
        }

        var todoPath = Path.Combine(root, "ToDo.txt");
        var todoText = await new AskUserForTask().GetToDoAsync();
        //var todoText = await new ToDoFileReader().GetToDoAsync(todoPath);

        Console.WriteLine($"\nProject root: {root}");
        Console.WriteLine($"Project type: {projectKind}");
        Console.WriteLine($"Project file: {(String.IsNullOrWhiteSpace(projectFile) ? "(no solution/project file)" : projectFile)}");
        Console.WriteLine($"ToDo:           {todoPath}");
        Console.WriteLine("\n--- ToDo.txt ---");
        Console.WriteLine(todoText);
        Console.WriteLine("----------------\n");

        var chatClient = new ChatClientProvider().Get("http://localhost:11434", "qwen2.5-coder:14b");

        var systemPrompt = """
    You are an autonomous coding agent working ONLY inside the supplied project root.
    The project may be Python, .NET, or another source-code project. The user supplied ToDo.txt as the task specification.

    CRITICAL RULE: You are a coding agent with a STRICT, CLOSED set of tools. NEVER turn task descriptions, todo items, or specific actions from ToDo.txt (such as 'fix_timestep_schedule' or similar) into tool names. You must ONLY choose from the exact supported tool names listed below.

    Each response must be exactly ONE JSON object, without Markdown fences or extra commentary:
    {"name":"TOOL_NAME","arguments":{...}}

    SUPPORTED TOOLS (Use ONLY these exact names, nothing else):
    - list_files: {"path":"."} (lists the immediate contents of a directory; use a relative path)
    - search_text: {"query":"text to search for"}
    - read_file: {"path":"relative/file/path"}
    - write_file: {"path":"relative/file/path","content":"complete new file content"}
    - check_for_missing_files: {"file_paths":["relative/path","another/file"]} (absolute paths are also accepted for this tool only when they are inside the project root)
    - build_project: {}
    - run_tests: {}
    - finish: {"summary":"what was changed and whether validation passed"}

    IMPORTANT: The JSON property must be named "name" and the parameters object must be named "arguments". Never use "function_name", "function", "tool_name", "parameters", "args", "directory", or placeholder names like TOOL_NAME.
    Do not invent tools. If a tool returns an error, read the error and choose a supported tool or finish if blocked.

    Workflow:
    1. Inspect the root with list_files using {"path":"."}. ToDo.txt has already been provided in the conversation; use that specification.
    2. Inspect relevant files and project configuration before editing.
    3. Make the smallest correct changes; do not make unrelated refactors.
    4. For Python, inspect pyproject.toml, requirements.txt and tests when present. Do not assume pytest is installed.
    5. Use paths relative to the project root for all tools other than check_for_missing_files. Never access files outside the root.
    6. write_file requires the user to approve each change. If rejected, do not pretend it was applied.
    7. After editing, call build_project. For Python this runs a syntax compilation check; for .NET it runs dotnet build. Run tests when relevant and inspect their output.
    8. If the same tool call returns the same result repeatedly, do not repeat it; choose a different action.
    9. When completed, or blocked, call finish with a concise factual summary.

    DO NOT call the finish tool just because you received an error message! Read the error, fix the call or switch to another tool, and continue working until the task is fully resolved.
    """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User,
                $"Project root: {root}\nProject kind: {projectKind}\nProject file: {(String.IsNullOrWhiteSpace(projectFile) ? "(none)" : projectFile)}\n\n" +
                "ToDo.txt content follows. Treat it as the requested work. Start by inspecting the project structure and relevant configuration files.\n--- BEGIN TODO ---\n" +
                todoText + "\n--- END TODO ---\nReturn one JSON tool call only.")
        };

        const int maxSteps = 30;
        string? lastActionKey = null;
        var consecutiveSameAction = 0;

        string? lastToolResult = null;
        var consecutiveSameResult = 0;

        for (var step = 1; step <= maxSteps; step++)
        {
            string modelOutput;
            try
            {
                var response = await chatClient.GetResponseAsync(messages);
                modelOutput = response.Text?.Trim() ?? String.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba az Ollama hívásakor: {ex.Message}");
                break;
            }

            Console.WriteLine($"\n[Agent {step}/{maxSteps}]\n{modelOutput}\n");
            messages.Add(new ChatMessage(ChatRole.Assistant, modelOutput));

            if (!TryParseAction(modelOutput, out var action, out var parseError))
            {
                messages.Add(new ChatMessage(ChatRole.User,
                    $"HOST ERROR: {parseError}\nReturn exactly one valid JSON object in the required format. No markdown or extra text."));
                continue;
            }

            var toolName = NormalizeToolName(action.Name);

            // ÚJ: Hoszt oldali eszköznév-ellenőrzés (Whitelist validáció)
            if (!ValidTools.Contains(toolName))
            {
                var errorMsg = $"HOST ERROR: '{action.Name}' nem létező vagy érvénytelen eszköznév. " +
                               $"Szigorúan csak az alábbi támogatott eszközök egyikét használhatod: {String.Join(", ", ValidTools)}.";

                Console.WriteLine($"\n[Hoszt szűrő] {errorMsg}\n");
                messages.Add(new ChatMessage(ChatRole.User, errorMsg + "\nJavítsd ki a hívást, és adj vissza pontosan egy érvényes JSON objektumot a megengedett eszközök valamelyikével."));
                continue;
            }

            var actionKey = toolName + ":" + action.Arguments.GetRawText();
            if (String.Equals(actionKey, lastActionKey, StringComparison.Ordinal))
                consecutiveSameAction++;
            else
                consecutiveSameAction = 1;
            lastActionKey = actionKey;

            if (consecutiveSameAction >= 3)
            {
                Console.WriteLine($"Az agent megakadt: ugyanazt a műveletet ({toolName}) háromszor egymás után kérte. Biztonsági okból leállítottam a ciklust.");
                break;
            }

            if (toolName.Equals("finish", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("=== Agent összegzése ===");
                Console.WriteLine(GetString(action.Arguments, "summary"));
                return;
            }

            string toolResult;
            try
            {
                toolResult = await ExecuteToolAsync(root, projectFile, projectKind, toolName, action.Arguments);
                Console.WriteLine($"[Tool result: {toolName}]\n{Limit(toolResult, maxLength)}");
            }
            catch (Exception ex)
            {
                toolResult = $"ERROR: {ex.GetType().Name}: {ex.Message}";
                Console.Error.WriteLine($"[Tool result: {toolName}]\n{Limit(toolResult, maxLength)}");
            }

            // --- ÚJ: Ugyanazon eredmény vizsgálata 3 alkalom után ---
            if (lastToolResult is not null && String.Equals(toolResult, lastToolResult, StringComparison.Ordinal))
            {
                consecutiveSameResult++;
            }
            else
            {
                consecutiveSameResult = 1;
                lastToolResult = toolResult;
            }

            //if (consecutiveSameResult >= 3)
            //{
            //    Console.WriteLine($"\n[Biztonsági leállítás] Az eszköz ugyanazt az eredményt adta vissza 3 alkalommal egymás után. Az agent elakadt (Loop detektálva).");
            //    break;
            //}

            messages.Add(new ChatMessage(ChatRole.User,
                $"Tool result for '{toolName}':\n{Limit(toolResult, 8000)}\n\nUse the result above. Do not repeat the same action unless something has changed. Return one JSON tool call only."));

            // Keep the system instructions and original task, but trim old tool chatter
            // so a long repair session does not balloon the context until Ollama times out.
            if (messages.Count > 22)
                messages.RemoveRange(2, messages.Count - 14);
        }

        Console.WriteLine($"Az agent leállt. Maximális lépésszám: {maxSteps}; ellenőrizd a fenti eredményeket.");
    }

    private static readonly HashSet<string> ValidTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "list_files", "search_text", "read_file", "write_file",
        "check_for_missing_files", "build_project", "run_tests", "finish"
    };



    private static bool TryParseAction(string output, out ParsedAction action, out string error)
    {
        action = new ParsedAction(String.Empty, default);
        error = String.Empty;

        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            error = "A modell válasza nem tartalmazott JSON-objektumot.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(output[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "A modell válaszának JSON-gyökere nem objektum.";
                return false;
            }

            // ITT A LÉNYEG: Hozzáadtuk a "tool_call" kulcsot a keresett mezőkhöz!
            string? name = GetFirstString(root, "name", "function_name", "tool_name", "tool", "tool_call");
            JsonElement functionElement = default;
            if (root.TryGetProperty("function", out functionElement))
            {
                if (functionElement.ValueKind == JsonValueKind.String)
                    name ??= functionElement.GetString();
                else if (functionElement.ValueKind == JsonValueKind.Object)
                    name ??= GetFirstString(functionElement, "name", "function_name", "tool_name", "tool_call");
            }

            if (String.IsNullOrWhiteSpace(name))
            {
                error = "A JSON-ból hiányzik a függvény neve (name vagy tool_call).";
                return false;
            }

            // Opcionális, de ajánlott: Ellenőrizzük, hogy a megadott eszköz létezik-e a whitelistben
            var normalizedCheck = NormalizeToolName(name);
            if (!ValidTools.Contains(normalizedCheck))
            {
                error = $"Ismeretlen eszköznév: '{name}'. A megengedett eszközök: {String.Join(", ", ValidTools)}";
                return false;
            }

            JsonElement argsElement = default;
            var hasArgs = root.TryGetProperty("arguments", out argsElement) ||
                          root.TryGetProperty("parameters", out argsElement) ||
                          root.TryGetProperty("args", out argsElement) ||
                          root.TryGetProperty("params", out argsElement);

            if (!hasArgs && functionElement.ValueKind == JsonValueKind.Object)
                hasArgs = functionElement.TryGetProperty("arguments", out argsElement) ||
                          functionElement.TryGetProperty("parameters", out argsElement) ||
                          functionElement.TryGetProperty("args", out argsElement) ||
                          functionElement.TryGetProperty("params", out argsElement);

            if (hasArgs && argsElement.ValueKind == JsonValueKind.Object)
            {
                action = new ParsedAction(name!, argsElement.Clone());
            }
            else
            {
                using var emptyDocument = JsonDocument.Parse("{}");
                action = new ParsedAction(name!, emptyDocument.RootElement.Clone());
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Érvénytelen JSON: {ex.Message}";
            return false;
        }
    }

    private static string? GetFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var result = value.GetString();
                if (!String.IsNullOrWhiteSpace(result))
                    return result;
            }
        }

        return null;
    }

    private static string NormalizeToolName(string name) => name.Trim().ToLowerInvariant() switch
    {
        "list_files" or "list_directory" or "list_dir" => "list_files",
        "search_text" or "search_files" => "search_text",
        "read_file" or "get_file_contents" => "read_file",
        "write_file" or "edit_file" or "update_file" => "write_file",

        "check_file_exists" or "check_file" or "file_exists" => "check_for_missing_files",

        "check_for_missing_files" or "check_missing_files" => "check_for_missing_files",
        "build_solution" or "build_project" => "build_project",
        "run_tests" or "test_project" => "run_tests",
        "finish" or "complete" => "finish",
        _ => name.Trim()
    };

    private static async Task<string> ExecuteToolAsync(
        string root, string projectFile, string projectKind, string toolName, JsonElement args)
    {
        switch (toolName)
        {
            case "list_files":
                {
                    var relativePath = GetString(args, "path", GetString(args, "directory", "."));
                    if (Path.IsPathRooted(relativePath))
                        relativePath = Path.GetRelativePath(root, Path.GetFullPath(relativePath));
                    var directory = ResolveWithinRoot(root, relativePath);
                    if (!Directory.Exists(directory))
                        return $"ERROR: A könyvtár nem található: {relativePath}";

                    // List only one level. Recursive 250-file dumps led the small model to
                    // repeatedly request the same truncated root listing.
                    var entries = new List<string>();
                    try
                    {
                        entries.AddRange(Directory.EnumerateDirectories(directory)
                            .Where(child => !ProjectITypedentifier.ExcludedDirectories.Contains(Path.GetFileName(child)))
                            .Select(child => "[DIR]  " + ToRelativePath(root, child) + "/"));
                        entries.AddRange(Directory.EnumerateFiles(directory)
                            .Select(file => "[FILE] " + ToRelativePath(root, file)));
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        return $"ERROR: Cannot list directory: {ex.Message}";
                    }

                    entries = entries.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    var total = entries.Count;
                    var truncated = entries.Take(160).ToList();
                    return $"Directory: {ToRelativePath(root, directory)}; entries: {total}" +
                           (total > truncated.Count ? $" (showing first {truncated.Count}; inspect subdirectories with list_files)" : String.Empty) +
                           "\n" + String.Join(Environment.NewLine, truncated);
                }

            case "check_for_missing_files":
                {
                    if (!args.TryGetProperty("file_paths", out var paths) || paths.ValueKind != JsonValueKind.Array)
                        return "ERROR: check_for_missing_files requires file_paths as an array of path strings.";

                    var statuses = new List<string>();
                    foreach (var item in paths.EnumerateArray().Take(100))
                    {
                        if (item.ValueKind != JsonValueKind.String) continue;
                        var suppliedPath = item.GetString() ?? String.Empty;
                        try
                        {
                            var file = ResolveToolPath(root, suppliedPath, allowAbsolute: true);
                            statuses.Add($"{(File.Exists(file) || Directory.Exists(file) ? "EXISTS" : "MISSING")}: {ToRelativePath(root, file)}");
                        }
                        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
                        {
                            statuses.Add($"BLOCKED: {suppliedPath} ({ex.Message})");
                        }
                    }

                    return String.Join(Environment.NewLine, statuses);
                }

            case "read_file":
                {
                    var relativePath = GetString(args, "path", GetString(args, "file_path"));
                    var file = ResolveWithinRoot(root, relativePath);
                    if (!File.Exists(file)) return $"ERROR: File not found: {relativePath}";
                    var info = new FileInfo(file);
                    if (info.Length > 300_000)
                        return "ERROR: File is larger than 300 KB. Search for the relevant section instead.";

                    return Limit(await File.ReadAllTextAsync(file), 30000);
                }

            case "search_text":
                {
                    var query = GetString(args, "query");
                    if (String.IsNullOrWhiteSpace(query)) return "ERROR: query must not be empty.";
                    if (query.Length < 2) return "ERROR: Use a search query of at least two characters.";

                    var matches = new List<string>();
                    var directories = new Stack<string>();
                    directories.Push(root);
                    var scanned = 0;
                    while (directories.Count > 0 && scanned < 5000 && matches.Count < 100)
                    {
                        var current = directories.Pop();
                        try
                        {
                            foreach (var child in Directory.EnumerateDirectories(current))
                            {
                                if (!ProjectITypedentifier.ExcludedDirectories.Contains(Path.GetFileName(child))) directories.Push(child);
                            }

                            foreach (var file in Directory.EnumerateFiles(current))
                            {
                                if (++scanned > 5000 || matches.Count >= 100) break;
                                if (!TextExtensions.Contains(Path.GetExtension(file)) || new FileInfo(file).Length > 500_000) continue;

                                try
                                {
                                    var lineNumber = 0;
                                    foreach (var line in File.ReadLines(file))
                                    {
                                        lineNumber++;
                                        if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                                        {
                                            matches.Add($"{ToRelativePath(root, file)}:{lineNumber}: {line.Trim()}");
                                            if (matches.Count >= 100) break;
                                        }
                                    }
                                }
                                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException) { }
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                    }

                    return $"Scanned approximately {scanned} files. Matches: {matches.Count}{(matches.Count >= 100 ? " (limit reached)" : String.Empty)}\n" +
                           (matches.Count == 0 ? "No matches found." : String.Join(Environment.NewLine, matches));
                }

            case "write_file":
                {
                    var relativePath = GetString(args, "path", GetString(args, "file_path"));
                    var content = GetString(args, "content", GetString(args, "new_content"));
                    var file = ResolveWithinRoot(root, relativePath);
                    var folder = Path.GetDirectoryName(file)!;
                    Directory.CreateDirectory(folder);

                    var exists = File.Exists(file);
                    var oldText = exists ? await File.ReadAllTextAsync(file) : String.Empty;
                    Console.WriteLine($"\n=== JÓVÁHAGYÁS SZÜKSÉGES ===\nFájl: {ToRelativePath(root, file)}\n" +
                                      $"Művelet: {(exists ? "meglévő fájl cseréje" : "új fájl létrehozása")}\n" +
                                      $"Régi méret: {oldText.Length} karakter; új méret: {content.Length} karakter\n" +
                                      "Új tartalom első 25 sora:\n" + String.Join(Environment.NewLine, content.Split('\n').Take(25)) +
                                      (content.Split('\n').Length > 25 ? "\n..." : String.Empty));
                    Console.Write("Alkalmazzam ezt a módosítást? Írd be pontosan: YES: ");
                    var approval = Console.ReadLine();
                    if (!String.Equals(approval, "YES", StringComparison.Ordinal))
                        return "WRITE REJECTED: the user did not type YES. The file was not changed.";

                    if (exists)
                    {
                        var backupRoot = Path.Combine(root, ".codingagent-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                        var backupPath = Path.Combine(backupRoot, ToRelativePath(root, file));
                        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                        await File.WriteAllTextAsync(backupPath, oldText, new UTF8Encoding(false));
                    }

                    await File.WriteAllTextAsync(file, content, new UTF8Encoding(false));
                    return $"WRITE SUCCESS: {ToRelativePath(root, file)} was {(exists ? "updated" : "created")}." +
                           (exists ? " A korábbi verzió mentése a .codingagent-backups mappába került." : String.Empty);
                }

            case "build_solution":
            case "build_project":
                return await BuildProjectAsync(root, projectFile, projectKind);

            case "run_tests":
                return await RunTestsAsync(root, projectFile, projectKind);

            default:
                return $"ERROR: Unknown tool '{toolName}'. Choose one of: list_files, search_text, read_file, write_file, build_project, run_tests, finish.";
        }
    }

    private static async Task<string> BuildProjectAsync(string root, string projectFile, string projectKind)
    {
        if (projectKind.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return await RunDotnetAsync(root, projectFile, "build");

        if (projectKind.Equals("python", StringComparison.OrdinalIgnoreCase))
        {
            var result = await ValidatePythonSyntaxAsync(root);
            return "Python AST syntax validation (no bytecode files are written)\n" + result;
        }

        return "ERROR: The project type could not be detected. No build command was run. " +
               "Inspect the project files and make sure the root contains a .py file, pyproject.toml, requirements.txt, or .csproj.";
    }

    private static async Task<string> ValidatePythonSyntaxAsync(string root)
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

        return await RunPythonAsync(root, new[] { "-c", script });
    }

    private static async Task<string> RunTestsAsync(string root, string projectFile, string projectKind)
    {
        if (projectKind.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return await RunDotnetAsync(root, projectFile, "test");

        if (projectKind.Equals("python", StringComparison.OrdinalIgnoreCase))
        {
            var pytestResult = await RunPythonAsync(root, new[] { "-m", "pytest", "-q" });
            if (pytestResult.Contains("No module named pytest", StringComparison.OrdinalIgnoreCase) ||
                pytestResult.Contains("No module named 'pytest'", StringComparison.OrdinalIgnoreCase))
            {
                return "pytest is not installed; falling back to standard-library unittest discovery.\n" +
                       await RunPythonAsync(root, new[] { "-m", "unittest", "discover", "-v" });
            }

            return pytestResult;
        }

        return "ERROR: The project type could not be detected. No tests were run.";
    }

    private static async Task<string> RunDotnetAsync(string root, string projectFile, string command)
    {
        var args = new List<string> { command };
        if (!String.IsNullOrWhiteSpace(projectFile))
            args.Add(projectFile);

        return await RunProcessAsync("dotnet", root, args, $"dotnet {command} {projectFile}".Trim());
    }

    private static async Task<string> RunPythonAsync(string root, IReadOnlyList<string> args)
    {
        try
        {
            return await RunProcessAsync("python", root, args, "python " + String.Join(" ", args));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            var pyArgs = new List<string> { "-3" };
            pyArgs.AddRange(args);
            return await RunProcessAsync("py", root, pyArgs, "py " + String.Join(" ", pyArgs));
        }
    }

    private static async Task<string> RunProcessAsync(
        string executable, string workingDirectory, IEnumerable<string> arguments, string displayCommand)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return $"ERROR: {displayCommand} timed out after 3 minutes; the process was stopped.";
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = (stdout + Environment.NewLine + stderr).Trim();
        return $"Command: {displayCommand}\nExit code: {process.ExitCode}\n" +
               (String.IsNullOrWhiteSpace(output) ? "(no output)" : Limit(output, maxLength));
    }

    private static string ResolveToolPath(string root, string suppliedPath, bool allowAbsolute)
    {
        if (String.IsNullOrWhiteSpace(suppliedPath))
            throw new ArgumentException("A file path is required.");

        if (!Path.IsPathRooted(suppliedPath))
            return ResolveWithinRoot(root, suppliedPath);

        if (!allowAbsolute)
            throw new InvalidOperationException("Absolute paths are not allowed for this operation.");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(suppliedPath);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("The path is outside the project root.");

        return ResolveWithinRoot(root, relative);
    }

    private static string ResolveWithinRoot(string root, string relativePath)
    {
        if (String.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A relative path is required.");
        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("Absolute paths are not allowed. Use a path relative to the solution root.");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Access outside the solution root is not allowed.");

        var parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(ProjectITypedentifier.ExcludedDirectories.Contains))
            throw new InvalidOperationException("Paths inside build-output or internal folders (bin, obj, .git, .vs, node_modules, backups) are blocked.");

        return fullPath;
    }

    private static string ToRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string GetString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "\n...[truncated]";

    private sealed record ParsedAction(string Name, JsonElement Arguments);
}
