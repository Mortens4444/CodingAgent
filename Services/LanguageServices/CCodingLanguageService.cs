using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class CCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public async Task<string> BuildAsync()
    {
        if (File.Exists(Path.Combine(Root, "CMakeLists.txt")))
        {
            var cmakeResult = await RunProcessAsync("cmake", Root, ["-B", "build", "-S", "."], "cmake -B build -S .");
            if (!cmakeResult.Contains("Exit code: 0")) return cmakeResult;
            return await RunProcessAsync("cmake", Root, ["--build", "build"], "cmake --build build");
        }
        if (File.Exists(Path.Combine(Root, "Makefile")) || File.Exists(Path.Combine(Root, "makefile")))
        {
            return await RunProcessAsync("make", Root, [], "make");
        }

        // Fallback: compile all .c files in root
        return await RunProcessAsync("gcc", Root, ["*.c", "-o", "program", "-Wall", "-Wextra"], "gcc *.c -o program");
    }

    public Task<string> RunTestsAsync() =>
        File.Exists(Path.Combine(Root, "test")) || File.Exists(Path.Combine(Root, "test.exe"))
            ? RunProcessAsync(Path.Combine(Root, "test"), Root, [], "./test")
            : File.Exists(Path.Combine(Root, "program"))
                ? RunProcessAsync(Path.Combine(Root, "program"), Root, [], "./program")
                : Task.FromResult("No compiled executable found to run tests.");

    public Task<string> ValidateSyntaxAsync() =>
        RunProcessAsync("gcc", Root, ["-fsyntax-only", "*.c"], "gcc -fsyntax-only *.c");
}

public class CppCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public async Task<string> BuildAsync()
    {
        if (File.Exists(Path.Combine(Root, "CMakeLists.txt")))
        {
            var cmakeResult = await RunProcessAsync("cmake", Root, ["-B", "build", "-S", "."], "cmake -B build -S .");
            if (!cmakeResult.Contains("Exit code: 0")) return cmakeResult;
            return await RunProcessAsync("cmake", Root, ["--build", "build"], "cmake --build build");
        }
        if (File.Exists(Path.Combine(Root, "Makefile")) || File.Exists(Path.Combine(Root, "makefile")))
        {
            return await RunProcessAsync("make", Root, [], "make");
        }

        return await RunProcessAsync("g++", Root, ["*.cpp", "-o", "program", "-std=c++17", "-Wall"], "g++ *.cpp -o program");
    }

    public Task<string> RunTestsAsync() =>
        File.Exists(Path.Combine(Root, "program"))
            ? RunProcessAsync(Path.Combine(Root, "program"), Root, [], "./program")
            : Task.FromResult("No compiled program found.");

    public Task<string> ValidateSyntaxAsync() =>
        RunProcessAsync("g++", Root, ["-fsyntax-only", "-std=c++17", "*.cpp"], "g++ -fsyntax-only *.cpp");
}