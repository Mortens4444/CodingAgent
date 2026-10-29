using CodingAgent.Interfaces;

namespace CodingAgent.Services.ToDo;

internal class AskUserForTask : IToDoProvider
{
    public async Task<string?> GetToDoAsync(string? todoPath = null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("How can I help you?");
        Console.ResetColor();
        return Console.ReadLine();
    }
}
