# Development environments

The agent detects the project language from the folder it is pointed at, then runs that language's
tools for `build_project` and `run_tests`. At startup it prints which tools are installed and how to
install the missing ones. When a tool is missing, the build/test step returns an error with an
install hint instead of failing silently, and the model is told not to retry.

## Quick setup (Windows)

```powershell
# Everything (skips tools already on PATH):
./scripts/setup-dev-env.ps1 -Languages All -IncludeOptional

# Only what you need; -WhatIf shows what would be installed:
./scripts/setup-dev-env.ps1 -Languages C,Cpp,JavaScript -WhatIf
```

Open a new terminal afterwards so the updated PATH is picked up. If the agent is already running, it can
call `check_environment`, which reloads PATH from the registry and re-checks the tools.

## Quick setup (Linux / macOS)

```bash
# Uses apt, dnf, pacman or Homebrew, whichever is installed; tools already on PATH are skipped.
./scripts/setup-dev-env.sh All --include-optional

# Only what you need; --dry-run shows the commands without running them:
./scripts/setup-dev-env.sh --dry-run C Cpp JavaScript
```

On macOS, gcc and make come from the Xcode command line tools (`xcode-select --install`). Restart the
agent from a new terminal afterwards.

## Language detection

| Language   | Detected by |
|------------|-------------|
| C#         | `.sln`, `.slnx`, `.csproj` (always wins) |
| Python     | `.py`, `pyproject.toml`, `requirements.txt`, `setup.py` |
| C          | `.c` (`.h`, `CMakeLists.txt`, `Makefile` only when nothing else matches) |
| C++        | `.cpp`, `.cc`, `.cxx`, `.hpp` |
| Java       | `.java`, `pom.xml`, `build.gradle(.kts)` |
| JavaScript | `.js`, `.mjs` (`package.json` only when nothing else matches) |
| TypeScript | `.ts`, `.tsx`, `tsconfig.json` |
| HTML       | `.html`, `.htm` |
| Assembly   | `.asm` (NASM), `.s` (GNU as) |
| Pascal     | `.pas`, `.pp` |
| Lisp       | `.lisp`, `.lsp`, `.cl` |

Build descriptors count more than single source files, so a TypeScript project with a few `.js`
config files is still TypeScript.

## What each language runs

| Language   | build_project | run_tests | Tools (Windows install) |
|------------|---------------|-----------|-------------------------|
| C# | `dotnet build -warnaserror` | `dotnet test` with coverage | `winget install Microsoft.DotNet.SDK.10` |
| Python | AST syntax check of all files | `pytest`, falls back to `unittest` | `winget install Python.Python.3.12` |
| C / C++ | CMake, else make, else `gcc`/`g++` over all non-test sources | `ctest`, else `make test`, else compiles `tests/` with the non-`main` sources and runs it | `winget install BrechtSanders.WinLibs.POSIX.UCRT` (+ optional `Kitware.CMake`, `ezwinports.make`) |
| Java | `mvnw`/`mvn compile`, `gradlew`/`gradle classes`, else `javac -d build/classes` | Maven/Gradle `test`, else the JUnit console launcher, else test classes with `main` | `winget install Microsoft.OpenJDK.21`; Maven/Gradle via project wrapper or scoop |
| JavaScript | `npm run build`, else `node --check` on each file | `npm test`, else `node --test` | `winget install OpenJS.NodeJS.LTS` |
| TypeScript | `npm run build`, else `tsc` | `npm test`, else `node --test` | Node.js; `npm i -D typescript` (otherwise `npx -p typescript` is used) |
| HTML | validation only | `npm test` when defined | none; `htmlhint` is used when configured, otherwise a built-in tag check |
| Assembly | `nasm -f win64/elf64` each `.asm`, link with `gcc` | runs `program` | `winget install NASM.NASM` + gcc |
| Pascal | `fpc` on the `.lpr`, `main.pas`, or first `program` file | compiles and runs `tests/test.pas` (or `test.pas`) | `winget install FreePascal.FreePascalCompiler` |
| Lisp | ASDF `load-system` of `<name>.asd`, else loads `main.lisp` (or all sources) in SBCL | ASDF `test-system`, else `sbcl --script` on `test.lisp` / `tests/*.lisp` | `winget install SBCL.SBCL` |

Linux and macOS install commands (apt / brew) are shown by the agent's startup check, and
`scripts/setup-dev-env.sh` runs them.

## Conventions for projects without a build system

- Test sources live in `test/` or `tests/`, or are named `test_*` / `*_test` / `test.*`.
- C/C++: test files must define their own `main`; project files that define `main` are left out of the test build.
- Lisp tests should exit with a non-zero code on failure (for example `(sb-ext:exit :code 1)`).
- Java without Maven/Gradle: put `junit-platform-console-standalone-<version>.jar` into `lib/` (or `libs/`,
  or the root) to run JUnit 5 tests; it is also put on the compile class path. Without it, every test class
  (`*Test`, `*Tests`, `Test*`, or under `test/`/`tests/`) that has a `main` method runs and must exit
  non-zero on failure. Test classes run with the `java` next to the `javac` found on PATH, so an older JRE
  earlier on PATH does not break them.
- Lisp with an ASDF system: `<name>.asd` in the root (the shortest name wins, so `app.asd` beats
  `app-test.asd`). Its test-op must signal an error when a test fails, or the run counts as passed.
  Quicklisp is loaded when installed, so dependencies such as FiveAM are downloaded: from the folder in the
  `QUICKLISP_HOME` environment variable, else `~/quicklisp`. To install it elsewhere (for example on J:),
  load https://beta.quicklisp.org/quicklisp.lisp in SBCL, run `(quicklisp-quickstart:install :path "J:/Tools/quicklisp/")`
  and set `QUICKLISP_HOME` to that folder.
- Build output goes to `build/` (or `program`/`program.exe` in the project root), which the agent's file tools ignore.
