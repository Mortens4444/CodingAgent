# Using the coding agent

The agent is a console app that runs a local model through [Ollama](https://ollama.com). You give it
a project folder; it reads the `ToDo.txt` in that folder as its task and then works on the project with a
fixed set of tools (read, edit, build, test, ...) until it calls `finish` or runs out of steps.

## Requirements

- .NET 10 SDK
- Ollama running locally (default `http://localhost:11434`). The model is pulled automatically on first
  start if it is not installed yet.
- The toolchain of the target project's language. The agent prints what is missing at startup; see
  [DEV_ENVIRONMENT.md](DEV_ENVIRONMENT.md) and `scripts/setup-dev-env.ps1`.

## Running it

```powershell
dotnet run --project CodingAgent.csproj
```

The agent asks for a target:

```
Specify the project directory or the path to the .sln/.slnx/.csproj file: J:\Projects\MyApp
```

You can give a folder, a `.sln`/`.slnx`/`.csproj` file, or a single source file (its folder becomes the
project root). It then prints the detected root, language, project file, the `ToDo.txt` it found, and the
development-environment check, and starts working. Every model reply and tool result is printed, so you
can follow along. Stop it at any time with Ctrl+C.

If a folder holds more than one solution file, pass the one you want.

## Writing ToDo.txt

Put `ToDo.txt` in the project root. Its whole content is the task. If it is missing or empty, the agent
starts with "What do you suggest to do?".

The model is small and has a small context window, so the task should be concrete:

- One goal per line, starting with `-`. Keep the list short; split big jobs over several runs.
- Name files, classes, or folders when you know them ("add input validation to `Services/OrderService.cs`").
- Say what "done" means: "build must pass", "add NUnit tests for X", "all tests pass".
- Mention constraints: framework, libraries to use or avoid, files that must not change.

Good:

```
-Add a `Divide` method to `Calculator.cs` that throws `DivideByZeroException` for 0.
-Add NUnit tests for `Divide` in the existing test project. Build and tests must pass.
```

Too vague:

```
-make the app better
```

After a run, review the changes (for example with `git diff`), then update `ToDo.txt` for the next run.

## What the agent can do

| Tool | Purpose |
|------|---------|
| `list_files` | List one directory |
| `search_text` | Search file contents, or file names with a wildcard such as `*Tests.cs` |
| `read_file` | Read a file; large files come in pages |
| `replace_in_file` | Replace an exact snippet in an existing file (preferred for edits) |
| `write_file` | Create a file or replace a whole file |
| `delete_file`, `move_file` | Delete or move one file |
| `check_for_missing_files` | Check which paths exist |
| `build_project` | Build with the language toolchain |
| `check_syntax` | Fast syntax/compile check |
| `run_tests` | Run the project's tests (C# also reports coverage) |
| `run_command` | Run one program in the project root, after you approve it |
| `finish` | End the run with a summary |

Safety limits:

- All paths stay inside the project root. `bin`, `obj`, `.git`, `node_modules`, `build`, virtual
  environments and similar folders are hidden from the tools.
- `run_command` asks `Allow? [y/N]` in the console before every command (configurable, see below). It runs
  the program directly, without a shell.
- With `WriteFile:Backup` enabled, every overwritten file is copied to `.codingagent-backups/` first.

Built-in guards against a stuck or careless model:

- A `finish` right after file changes, without a build or test run, is refused once.
- The same call with the same arguments three times in a row is not run again.
- Five unusable replies in a row (no JSON, unknown tool) end the run.
- When the history no longer fits the context window, the oldest messages are dropped and a progress note
  (files changed, last build/test results) is kept instead.
- The run stops after 100 steps.

## Configuration

Settings live in `appsettings.json` and can be overridden on the command line
(`--CodingAgent:Model=qwen2.5-coder:7b`) or with environment variables (`CodingAgent__Model`).

| Setting | Default | Meaning |
|---------|---------|---------|
| `CodingAgent:Model` | `qwen2.5-coder:14b` | Ollama tag, or a name from `Enums/ChatClientType.cs` such as `Qwen2_5_coder_32b` |
| `CodingAgent:OllamaUrl` | `http://localhost:11434` | Ollama server |
| `CodingAgent:ContextLength` | `8192` | Context window passed to Ollama (`num_ctx`); larger helps if your GPU memory allows |
| `CodingAgent:TimeoutMinutes` | `30` | HTTP timeout per model call; `0` means none |
| `CodingAgent:WriteFile:Backup` | `false` | Back up files before overwriting them |
| `CodingAgent:RunCommand:Approval` | `Ask` | `Ask` each time, `Always` allow, or `Never` allow `run_command` |

Known models (`Enums/ChatClientType.cs`): `qwen2.5-coder:7b`, `qwen2.5-coder:14b`, `qwen2.5-coder:32b`,
`qwen3.5:9b`, `deepseek-coder:6.7b`, `deepseek-coder:33b`. Bigger models follow instructions better but
need more memory; `deepseek-coder:33b` needs about 19 GB.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `Could not prepare model ... on Ollama` | Start Ollama (`ollama serve`) or check `OllamaUrl` |
| `'gcc' was not found on PATH` (or another tool) | Install it (`scripts/setup-dev-env.ps1 -Languages C`) and open a new terminal |
| Agent loops or gives up early | Make `ToDo.txt` more specific, use a bigger model, or raise `ContextLength` |
| `Project type: Unknown` | Point it at the project file or a source file directly |

## Developing the agent

```powershell
dotnet build CodingAgent.slnx
dotnet test CodingAgent.slnx --collect:"XPlat Code Coverage"
```

Tests use NUnit and Mocklis (`CodingAgent.UnitTests/Mocks`). External processes go through
`IProcessRunner`, so language services are tested without installed compilers.

### End-to-end tests

`CodingAgent.UnitTests/Integration/HelloWorldAgentTests.cs` runs the real agent against Ollama once per
language: it starts from an empty folder with a `ToDo.txt` asking for a hello world app, tests, a user
`README.md` and a developer `DEVELOPMENT.md`, and then builds and tests the result itself and checks that both
documents exist and cover running, building and testing. These tests are explicit, so a normal `dotnet test` skips them:

```powershell
dotnet test CodingAgent.UnitTests --filter "TestCategory=Integration"
dotnet test CodingAgent.UnitTests --filter "TestCategory=Integration&Name~Python"
```

A language is skipped when its toolchain is missing, and every test is skipped when Ollama or the model is
not available. Settings come from environment variables: `CODINGAGENT_E2E_MODEL`, `CODINGAGENT_E2E_OLLAMA_URL`,
`CODINGAGENT_E2E_CONTEXT` and `CODINGAGENT_E2E_RUNCOMMAND` (default `Never`, so the model cannot start
programs). The generated projects stay in `%TEMP%\CodingAgentE2E` for inspection.

## Ideas for the next ToDo.txt

Candidates for future work on the agent itself:

- Linux/macOS version of `scripts/setup-dev-env.ps1` (currently install hints only).
- A `check_environment` tool, so the model can re-check the toolchain after the user installs something.
- Stream model output instead of waiting for the full reply.
- Optional approval for `write_file`/`delete_file`, and a summary diff of all changes at the end.
- Language-specific test setup (JUnit for plain Java, a Lisp test framework) when no build tool exists.
