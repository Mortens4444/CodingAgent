using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

// .asm files are NASM sources (win64 objects on Windows, elf64 elsewhere) linked with gcc; .s files go straight to gcc.
public class AssemblyCodingLanguageService(string root, IProcessRunner? processRunner = null) : LanguageService(root, processRunner), ICodingLanguageService
{
    private const string ObjectDirectory = "build/obj";
    private static readonly string Program = ExecutableName("program");

    private static string NasmFormat => IsWindows ? "win64" : "elf64";

    public async Task<string> BuildAsync()
    {
        if (HasFile("Makefile", "makefile"))
            return await RunProcessAsync("make", []);

        var nasmSources = FindSourceFiles(".asm");
        var gasSources = FindSourceFiles(".s");
        if (nasmSources.Count == 0 && gasSources.Count == 0)
            return NoSourcesMessage("assembly (.asm/.s)");

        Directory.CreateDirectory(Path.Combine(Root, ObjectDirectory));
        var objects = new List<string>();
        foreach (var source in nasmSources)
        {
            var objectFile = $"{ObjectDirectory}/{Path.GetFileNameWithoutExtension(source)}.o";
            var assemble = await RunProcessAsync("nasm", ["-f", NasmFormat, source, "-o", objectFile]);
            if (!Succeeded(assemble))
                return assemble;
            objects.Add(objectFile);
        }

        List<string> linkArguments = [.. objects, .. gasSources, "-o", Program];
        if (!IsWindows)
            linkArguments.Add("-no-pie");

        return await RunProcessAsync("gcc", linkArguments);
    }

    public Task<string> RunTestsAsync() =>
        RunExecutableAsync(Program, $"No {Program} found. Run build_project first.");

    public async Task<string> ValidateSyntaxAsync()
    {
        var nasmSources = FindSourceFiles(".asm");
        var gasSources = FindSourceFiles(".s");
        if (nasmSources.Count == 0 && gasSources.Count == 0)
            return NoSourcesMessage("assembly (.asm/.s)");

        var results = new List<string>();
        foreach (var source in nasmSources)
            results.Add(await RunProcessAsync("nasm", ["-f", NasmFormat, source, "-o", NullDevice]));
        foreach (var source in gasSources)
            results.Add(await RunProcessAsync("gcc", ["-c", source, "-o", NullDevice]));

        var failures = results.Where(result => !Succeeded(result)).ToList();
        return failures.Count == 0
            ? $"Command: assemble each file\nExit code: 0\nChecked {results.Count} assembly files."
            : String.Join("\n\n", failures);
    }
}
