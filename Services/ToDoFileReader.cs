using CodingAgent.Interfaces;

namespace CodingAgent.Services;

internal class ToDoFileReader : IToDoProvider
{
    public async Task<string?> GetToDoAsync(string? todoPath = null)
    {
        if (!File.Exists(todoPath))
        {
            Console.Error.WriteLine($"Can't find file ToDo.txt in the project root folfder: {todoPath}");
            return null;
        }

        var todoText = await File.ReadAllTextAsync(todoPath!);
        if (String.IsNullOrWhiteSpace(todoText))
        {
            Console.Error.WriteLine("ToDo.txt is empty.");
            return null;
        }

        return todoText;
    }
}
