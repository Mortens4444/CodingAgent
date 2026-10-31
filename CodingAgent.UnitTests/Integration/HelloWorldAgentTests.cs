using CodingAgent.Dto;
using CodingAgent.Enums;
using CodingAgent.Services;
using CodingAgent.Services.ChatClient;
using CodingAgent.Services.DevEnvironment;
using CodingAgent.Services.LanguageServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace CodingAgent.UnitTests.Integration;

// End-to-end runs of the real agent against a local Ollama model: for each language the agent starts from an
// empty folder and must create a hello world app plus tests, a user README.md and a developer DEVELOPMENT.md.
// Afterwards the test builds and tests the result itself and checks the documents, so a run only passes when the
// generated project really works, not when the model claims it does.
//
// Explicit, so a normal "dotnet test" skips them. Run them with:
//   dotnet test CodingAgent.UnitTests --filter "TestCategory=Integration"
//   dotnet test CodingAgent.UnitTests --filter "TestCategory=Integration&Name~Python"
// Environment variables: CODINGAGENT_E2E_MODEL (Ollama tag, default ChatClientFactory.DefaultModel),
// CODINGAGENT_E2E_OLLAMA_URL (default http://localhost:11434), CODINGAGENT_E2E_CONTEXT (default 8192),
// CODINGAGENT_E2E_RUNCOMMAND (run_command approval, default Never so the model cannot start arbitrary programs).
// Generated projects are kept under %TEMP%\CodingAgentE2E for inspection.
[TestFixture]
[Explicit("Needs a running Ollama server, the model and the language toolchains; runs for minutes per language.")]
[Category("Integration")]
[NonParallelizable]
public class HelloWorldAgentTests
{
    public sealed record Scenario(ProgrammingLanguage Language, string[] RequiredTools, string ToDo, string? ExpectedTestOutput = null)
    {
        public override string ToString() => Language.ToString();
    }

    private const string Greeting = "Hello, World!";

    // Appended to every scenario: the project must also be documented for its users and its developers.
    private const string DocsToDo = """
        -Create README.md for users: what the program does, the prerequisites, and the exact command to run it.
        -Create DEVELOPMENT.md for developers: the project layout (every source and test file), and the exact commands to build it and to run the tests.
        """;

    private static string FullToDo(Scenario scenario) => scenario.ToDo.TrimEnd() + "\n" + DocsToDo;

    public static IEnumerable<Scenario> Scenarios() =>
    [
        new(ProgrammingLanguage.CSharp, ["dotnet"], $"""
            -Create a .NET 10 console app in HelloWorld/HelloWorld.csproj. Add a public static class Greeter with a method GetGreeting() that returns "{Greeting}"; Program.cs prints Greeter.GetGreeting().
            -Create an NUnit test project in HelloWorld.Tests/HelloWorld.Tests.csproj that references HelloWorld/HelloWorld.csproj and tests that GetGreeting() returns "{Greeting}". Use the packages Microsoft.NET.Test.Sdk, NUnit and NUnit3TestAdapter.
            -Create HelloWorld.slnx in the project root that lists both projects.
            -Build and tests must pass.
            """),
        new(ProgrammingLanguage.Python, ["python"], $"""
            -Create hello.py with a function greet() that returns "{Greeting}" and prints it when run as a script.
            -Create tests/__init__.py (empty) and tests/test_hello.py with a unittest.TestCase that checks greet() returns "{Greeting}".
            -Use only the standard library. Tests must pass.
            """),
        new(ProgrammingLanguage.C, ["gcc"], $"""
            -Create greeting.h and greeting.c with a function const char *greeting(void) that returns "{Greeting}".
            -Create main.c whose main() prints greeting() followed by a newline.
            -Create tests/test_greeting.c with its own main() that checks greeting() with strcmp, prints PASS or FAIL, and returns 0 only when the check passes.
            -Do not add a Makefile or CMakeLists.txt. Build and tests must pass.
            """),
        new(ProgrammingLanguage.Cpp, ["g++"], $"""
            -Create greeting.hpp and greeting.cpp with a function std::string greeting() that returns "{Greeting}".
            -Create main.cpp whose main() prints greeting() followed by a newline.
            -Create tests/test_greeting.cpp with its own main() that checks greeting(), prints PASS or FAIL, and returns 0 only when the check passes.
            -Do not add a Makefile or CMakeLists.txt. Build and tests must pass.
            """),
        new(ProgrammingLanguage.Java, ["javac", "mvn"], $"""
            -Create a Maven project: pom.xml (groupId hello, artifactId hello-world, Java 17, JUnit Jupiter 5.10.2 test dependency, maven-surefire-plugin 3.2.5).
            -Create src/main/java/hello/HelloWorld.java with a static method greeting() that returns "{Greeting}" and a main method that prints it.
            -Create src/test/java/hello/HelloWorldTest.java with a JUnit 5 test that checks greeting() returns "{Greeting}".
            -Build and tests must pass.
            """),
        new(ProgrammingLanguage.JavaScript, ["node", "npm"], $"""
            -Create package.json (name hello-world, "type": "commonjs") with the script "test": "node --test" and no dependencies.
            -Create hello.js that exports a function greet() returning "{Greeting}" and prints it when run directly (require.main === module).
            -Create test/hello.test.js that uses node:test and node:assert to check greet() returns "{Greeting}".
            -Tests must pass.
            """),
        new(ProgrammingLanguage.TypeScript, ["node", "npm", "npx"], $"""
            -Create tsconfig.json with target ES2022, module commonjs, rootDir ".", outDir "dist", strict true, "types": [] and include ["src", "test"].
            -Create src/hello.ts that exports a function greet(): string returning "{Greeting}".
            -Create test/hello.test.ts that uses require("node:test") and require("node:assert") to check greet() returns "{Greeting}". Declare require with "declare const require: any;" so no @types packages are needed.
            -Create package.json (name hello-world) with the scripts "build": "npx --yes -p typescript tsc" and "test": "npx --yes -p typescript tsc && node --test dist/test/" and no dependencies.
            -Build and tests must pass.
            """),
        new(ProgrammingLanguage.Html, ["node", "npm"], $"""
            -Create index.html: a complete HTML5 page with the title "Hello" and an h1 that says "{Greeting}".
            -Create package.json (name hello-world) with the script "test": "node --test" and no dependencies.
            -Create test/index.test.js that uses node:test, node:assert and node:fs to read index.html and check that it contains "<h1>{Greeting}</h1>".
            -Validation and tests must pass.
            """),
        new(ProgrammingLanguage.Assembly, ["nasm", "gcc"], $"""
            -Create hello.asm, a NASM program for {(OperatingSystem.IsWindows() ? "64-bit Windows (win64 object, Microsoft x64 calling convention, reserve 32 bytes of shadow space)" : "64-bit Linux (elf64, System V calling convention)")} that defines main, calls the C function printf to print "{Greeting}" followed by a newline, and returns 0 from main.
            -It is linked with gcc; do not add a Makefile. Build must pass, and running the program (run_tests) must print "{Greeting}" and exit with code 0.
            """, Greeting),
        new(ProgrammingLanguage.Pascal, ["fpc"], $$"""
            -Create greeting.pas, a unit Greeting with a function GetGreeting: string that returns '{{Greeting}}'.
            -Create hello.lpr, a program that uses Greeting and writes GetGreeting.
            -Create tests/test.pas, a program that uses Greeting (add {$UNITPATH ..} so it finds the unit), checks GetGreeting, writes PASS or FAIL and calls Halt(1) on failure.
            -Build and tests must pass.
            """),
        new(ProgrammingLanguage.Lisp, ["sbcl"], $"""
            -Create main.lisp that defines a function greet returning "{Greeting}". Loading it must not print anything or exit.
            -Create tests/test.lisp that loads main.lisp relative to its own location ((load (merge-pathnames "../main.lisp" *load-truename*))), checks (string= (greet) "{Greeting}"), prints PASS or FAIL and exits with (sb-ext:exit :code 1) on failure and (sb-ext:exit :code 0) on success.
            -Build and tests must pass.
            """),
    ];

    [TestCaseSource(nameof(Scenarios))]
    public async Task Agent_CreatesHelloWorldAppWithTests(Scenario scenario)
    {
        var runner = ProcessRunner.Default;
        var missing = scenario.RequiredTools.Where(tool => runner.FindExecutable(tool) is null).ToList();
        if (missing.Count > 0)
            Assert.Ignore($"Toolchain not installed: {String.Join(", ", missing)}.");

        var model = Setting("CODINGAGENT_E2E_MODEL", ChatClientFactory.DefaultModel);
        var contextLength = Int32.Parse(Setting("CODINGAGENT_E2E_CONTEXT", Constants.DefaultContextLength.ToString()));
        using var ollama = ChatClientFactory.Get(Setting("CODINGAGENT_E2E_OLLAMA_URL", "http://localhost:11434"), model);
        await IgnoreUnlessModelAvailable(ollama, model);

        var root = Path.Combine(Path.GetTempPath(), "CodingAgentE2E", $"{scenario.Language}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(root);
        var todo = FullToDo(scenario);
        await File.WriteAllTextAsync(Path.Combine(root, "ToDo.txt"), todo);
        TestContext.Out.WriteLine($"Project folder: {root}");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CodingAgent:RunCommand:Approval"] = Setting("CODINGAGENT_E2E_RUNCOMMAND", "Never"),
                ["CodingAgent:WriteFile:Backup"] = "false"
            })
            .Build();

        using var chatClient = new ChatClientBuilder(ollama)
            .ConfigureOptions(options => ChatClientFactory.ConfigureOptions(options, model, contextLength))
            .Build();

        var project = new ProjectContext(root, String.Empty, scenario.Language, todo);
        var environment = DevEnvironmentChecker.Check(scenario.Language, runner);
        var loop = new CodingAgentLoop(chatClient, new ToolDispatcher(configuration, runner), contextLength);

        await loop.RunAsync(
            CodingAgentApplication.CreateLanguageService(project, runner),
            AgentPromptBuilder.CreateInitialMessages(project, environment.PromptNote));

        // Judge the result independently of what the agent reported. A .slnx the agent created becomes the C# project file.
        var detected = new ProjectIdentifier().ResolveProject(root);
        var projectFile = detected.ProgrammingLanguage == scenario.Language ? detected.ProjectFile : String.Empty;
        var languageService = CodingAgentApplication.CreateLanguageService(project with { ProjectFile = projectFile }, runner);

        var build = await languageService.BuildAsync();
        var tests = await languageService.RunTestsAsync();
        TestContext.Out.WriteLine($"=== Build ===\n{build}\n=== Tests ===\n{tests}");

        Assert.Multiple(() =>
        {
            Assert.That(AgentProgress.Passed(build), Is.True, "The generated project does not build:\n" + build);
            Assert.That(AgentProgress.Passed(tests), Is.True, "The generated tests do not pass:\n" + tests);
            if (scenario.ExpectedTestOutput is not null)
                Assert.That(tests, Does.Contain(scenario.ExpectedTestOutput));

            AssertDocument(root, "README.md", "run");
            AssertDocument(root, "DEVELOPMENT.md", "build", "test");
        });
    }

    // Only checks that the document exists, has some substance and covers its topics; the wording is up to the model.
    private static void AssertDocument(string root, string fileName, params string[] topics)
    {
        var path = Path.Combine(root, fileName);
        if (!File.Exists(path))
        {
            Assert.Fail($"{fileName} was not created.");
            return;
        }

        var text = File.ReadAllText(path);
        TestContext.Out.WriteLine($"=== {fileName} ===\n{text}");

        Assert.That(text.Trim(), Has.Length.GreaterThan(100), $"{fileName} is nearly empty.");
        foreach (var topic in topics)
            Assert.That(text, Does.Contain(topic).IgnoreCase, $"{fileName} does not mention '{topic}'.");
    }

    private static string Setting(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return String.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static async Task IgnoreUnlessModelAvailable(OllamaSharp.IOllamaApiClient ollama, string model)
    {
        IEnumerable<OllamaSharp.Models.Model> models;
        try
        {
            models = await ollama.ListLocalModelsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Ignore($"Ollama is not reachable: {ex.Message}");
            return;
        }

        var tag = model.Contains(':') ? model : model + ":latest";
        if (!models.Any(m => m.Name.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            Assert.Ignore($"Model '{model}' is not installed on Ollama (run: ollama pull {model}).");
    }
}
