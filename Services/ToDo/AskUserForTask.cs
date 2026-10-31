using CodingAgent.Interfaces;

namespace CodingAgent.Services.ToDo;

public class AskUserForTask : IToDoProvider
{
    public async Task<string?> GetToDoAsync(string? todoPath = null)
    {
        ConsoleOutput.WriteLine("How can I help you?", ConsoleOutput.Warning);
        return Console.ReadLine();
    }
}
