using CodingAgent;
using CodingAgent.Enums;
using CodingAgent.Extensions;
using CodingAgent.Interfaces;
using CodingAgent.Services;
using CodingAgent.Services.ChatClient;
using CodingAgent.Services.LanguageServices;
using CodingAgent.Services.ToDo;
using Microsoft.Extensions.AI;
using System.Text.Json;

internal static class Program
{
    private static readonly ToolDispatcher toolDispatcher = new();

    private static readonly HashSet<string> ValidTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "list_files", "search_text", "read_file", "write_file",
        "check_for_missing_files", "build_project", "run_tests", "finish"
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

        Console.Write("Specify the project directory or the path to the .sln/.slnx/.csproj file: ");
        var target = Console.ReadLine()?.Trim().Trim('"');

        if (String.IsNullOrWhiteSpace(target))
        {
            Console.WriteLine("You did not provide a route.");
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
        //var todoText = await new AskUserForTask().GetToDoAsync();
        var todoText = await new ToDoFileReader().GetToDoAsync(todoPath);

        Console.WriteLine($"\nProject root: {root}");
        Console.WriteLine($"Project type: {projectKind}");
        Console.WriteLine($"Project file: {(String.IsNullOrWhiteSpace(projectFile) ? "(no solution/project file)" : projectFile)}");
        Console.WriteLine($"ToDo:           {todoPath}");
        Console.WriteLine("\n--- ToDo.txt ---");
        Console.WriteLine(todoText);
        Console.WriteLine("----------------\n");

        ICodingLanguageService codingLanguageService = new CSharpCodingLanguageService(root, projectFile);
        //ICodingLanguageService codingLanguageService = new PythonCodingLanguagetService(root);

        //var chatClient = new ChatClientProvider().Get("http://localhost:11434", "qwen2.5-coder:14b");
        var chatClient = new ChatClientProvider().Get("http://localhost:11434", ChatClientFactory.GetChatClientModelName(ChatClientType.Qwen2_5_coder_14b));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, PromptProvider.SystemPrompt),
            new(ChatRole.User,
                $"Project root: {root}\nProject kind: {projectKind}\nProject file: {(String.IsNullOrWhiteSpace(projectFile) ? "(none)" : projectFile)}\n\n" +
                "ToDo.txt content follows. Treat it as the requested work. Start by inspecting the project structure and relevant configuration files.\n--- BEGIN TODO ---\n" +
                todoText + "\n--- END TODO ---\nReturn one JSON tool call only.")
        };

        string? lastActionKey = null;
        var consecutiveSameAction = 0;

        string? lastToolResult = null;
        var consecutiveSameResult = 0;

        for (var step = 1; step <= Constants.MaxSteps; step++)
        {
            string modelOutput;
            try
            {
                var response = await chatClient.GetResponseAsync(messages);
                modelOutput = response.Text?.Trim() ?? String.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when calling Ollama: {ex.Message}");
                break;
            }

            Console.WriteLine($"\n[Agent {step}/{Constants.MaxSteps}]\n{modelOutput}\n");
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
                var errorMsg = $"HOST ERROR: '{action.Name}' not existing or invalid tool name. " +
                               $"You may use strictly one of the supported devices listed below: {String.Join(", ", ValidTools)}.";

                Console.WriteLine($"\n[Host filter] {errorMsg}\n");
                messages.Add(new ChatMessage(ChatRole.User, errorMsg + "\nCorrect the call and return exactly one valid JSON object using one of the permitted tools."));
                continue;
            }

            var actionKey = toolName + ":" + action.Arguments.GetRawText();
            if (String.Equals(actionKey, lastActionKey, StringComparison.Ordinal))
                consecutiveSameAction++;
            else
                consecutiveSameAction = 1;
            lastActionKey = actionKey;

            //if (consecutiveSameAction >= 3)
            //{
            //    Console.WriteLine($"Az agent megakadt: ugyanazt a műveletet ({toolName}) háromszor egymás után kérte. Biztonsági okból leállítottam a ciklust.");
            //    break;
            //}

            if (toolName.Equals("finish", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("=== Agent summary ===");
                var summary = action.Arguments.GetString("summary");
                Console.WriteLine(summary);
                return;
            }

            string toolResult;
            try
            {
                toolResult = await toolDispatcher.ExecuteToolAsync(codingLanguageService, toolName, action.Arguments);
                Console.WriteLine($"[Tool result: {toolName}]\n{toolResult.Limit(Constants.MaxLength)}");
            }
            catch (Exception ex)
            {
                toolResult = $"ERROR: {ex.GetType().Name}: {ex.Message}";
                Console.Error.WriteLine($"[Tool result: {toolName}]\n{toolResult.Limit(Constants.MaxLength)}");
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
                $"Tool result for '{toolName}':\n{toolResult.Limit(8000)}\n\nUse the result above. Do not repeat the same action unless something has changed. Return one JSON tool call only."));

            // Keep the system instructions and original task, but trim old tool chatter
            // so a long repair session does not balloon the context until Ollama times out.
            if (messages.Count > 22)
                messages.RemoveRange(2, messages.Count - 14);
        }

        Console.WriteLine($"Agent stopped. Maximumu step count: {Constants.MaxSteps}; check the results above.");
    }

    private static bool TryParseAction(string output, out ParsedAction action, out string error)
    {
        action = new ParsedAction(String.Empty, default);
        error = String.Empty;

        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            error = "The model's response hasn't contain any JSON-object.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(output[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The model's response JSON-root is not an object.";
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
                error = "The function name is missing from the JSON (name or tool_call).";
                return false;
            }

            // Opcionális, de ajánlott: Ellenőrizzük, hogy a megadott eszköz létezik-e a whitelistben
            var normalizedCheck = NormalizeToolName(name);
            if (!ValidTools.Contains(normalizedCheck))
            {
                error = $"Unknown tool name: '{name}'. Allowed tools: {String.Join(", ", ValidTools)}";
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
            error = $"Invalid JSON: {ex.Message}";
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

    private sealed record ParsedAction(string Name, JsonElement Arguments);
}
