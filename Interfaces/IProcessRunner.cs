namespace CodingAgent.Interfaces;

public interface IProcessRunner
{
    // Returns the full path of an executable on PATH (honoring PATHEXT on Windows), or null when it is not installed.
    string? FindExecutable(string name);

    // Throws Win32Exception when the executable cannot be found or started.
    Task<string> RunAsync(string executable, string workingDirectory, IReadOnlyList<string> arguments, string displayCommand);
}
