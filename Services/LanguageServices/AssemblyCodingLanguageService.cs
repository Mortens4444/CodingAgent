using CodingAgent.Interfaces;

namespace CodingAgent.Services.LanguageServices;

public class AssemblyCodingLanguageService(string root) : LanguageService(root), ICodingLanguageService
{
    public async Task<string> BuildAsync()
    {
        var nasmResult = await RunProcessAsync("nasm", Root, ["-f", "elf64", "main.asm", "-o", "main.o"], "nasm -f elf64 main.asm -o main.o");
        if (!nasmResult.Contains("Exit code: 0")) return nasmResult;
        return await RunProcessAsync("gcc", Root, ["main.o", "-o", "program", "-no-pie"], "gcc main.o -o program -no-pie");
    }

    public Task<string> RunTestsAsync() => RunProcessAsync("./program", Root, [], "./program");

    public Task<string> ValidateSyntaxAsync() => RunProcessAsync("nasm", Root, ["-f", "elf64", "main.asm", "-o", "/dev/null"], "nasm -f elf64 main.asm -o /dev/null");
}