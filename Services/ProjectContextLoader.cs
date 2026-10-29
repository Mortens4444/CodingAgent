using CodingAgent.Interfaces;

namespace CodingAgent.Services;

internal sealed record ProjectContext(string Root, string ProjectFile, string ProjectKind, string TodoText);

internal sealed class ProjectContextLoader(IToDoProvider toDoProvider)
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

        string root;
        string projectFile;
        string projectKind;

        try
        {
            (root, projectFile, projectKind) =
                new ProjectITypedentifier().ResolveProject(target);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error detecting project: {ex.Message}");
            return null;
        }

        var todoPath = Path.Combine(root, "ToDo.txt");
        var todoText = await toDoProvider.GetToDoAsync(todoPath) ?? "What do you suggest to do?";

        Console.WriteLine($"\nProject root: {root}");
        Console.WriteLine($"Project type: {projectKind}");
        Console.WriteLine($"Project file: {(String.IsNullOrWhiteSpace(projectFile) ? "(no solution/project file)" : projectFile)}");
        Console.WriteLine($"ToDo:         {todoPath}");
        Console.WriteLine("\n--- ToDo.txt ---");
        Console.WriteLine(todoText);
        Console.WriteLine("----------------\n");

        return new ProjectContext(root, projectFile, projectKind, todoText);
    }
}