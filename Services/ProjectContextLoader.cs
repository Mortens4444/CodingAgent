using CodingAgent.Dto;
using CodingAgent.Interfaces;

namespace CodingAgent.Services;

public sealed class ProjectContextLoader(IToDoProvider toDoProvider)
{
    private readonly IToDoProvider toDoProvider = toDoProvider;

    public async Task<ProjectContext?> LoadAsync()
    {
        Console.Write("Specify the project directory or the path to the .sln/.slnx/.csproj file: ");

        var target = Console.ReadLine()?.Trim().Trim('"');

        if (String.IsNullOrWhiteSpace(target))
        {
            Console.WriteLine("You did not provide a path.");
            return null;
        }

        ResolvedProject resolved;

        try
        {
            resolved = new ProjectIdentifier().ResolveProject(target);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error detecting project: {ex.Message}");
            return null;
        }

        var todoPath = Path.Combine(resolved.Root, "ToDo.txt");
        var todoText = await toDoProvider.GetToDoAsync(todoPath) ?? "What do you suggest to do?";

        Console.WriteLine($"\nProject root: {resolved.Root}");
        Console.WriteLine($"Project type: {resolved.ProgrammingLanguage}");
        Console.WriteLine($"Project file: {(String.IsNullOrWhiteSpace(resolved.ProjectFile) ? "(no solution/project file)" : resolved.ProjectFile)}");
        Console.WriteLine($"ToDo:         {todoPath}");
        Console.WriteLine("\n--- ToDo.txt ---");
        Console.WriteLine(todoText);
        Console.WriteLine("----------------\n");

        return new ProjectContext(resolved.Root, resolved.ProjectFile, resolved.ProgrammingLanguage, todoText);
    }
}