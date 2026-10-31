using CodingAgent.Extensions;
using CodingAgent.Interfaces;
using System.ComponentModel;
using System.Diagnostics;

namespace CodingAgent.Services.LanguageServices;

public sealed class ProcessRunner(TimeSpan? timeout = null) : IProcessRunner
{
    public static ProcessRunner Default { get; } = new();

    private readonly TimeSpan timeout = timeout ?? TimeSpan.FromMinutes(3);

    public string? FindExecutable(string name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return null;

        // npm, npx, mvn and gradle are .cmd/.bat files on Windows; Process.Start only appends .exe on its own.
        var extensions = OperatingSystem.IsWindows() && !Path.HasExtension(name)
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [String.Empty];

        if (Path.IsPathRooted(name) || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
            return FirstExisting(Path.GetFullPath(name), extensions);

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? String.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in directories)
        {
            try
            {
                var found = FirstExisting(Path.Combine(directory.Trim('"'), name), extensions);
                if (found is not null) return found;
            }
            catch (ArgumentException) { }
        }

        return null;
    }

    public async Task<string> RunAsync(string executable, string workingDirectory, IReadOnlyList<string> arguments, string displayCommand)
    {
        var resolved = FindExecutable(executable)
            ?? throw new Win32Exception(2, $"'{executable}' was not found on PATH.");

        var startInfo = new ProcessStartInfo
        {
            FileName = resolved,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return $"ERROR: {displayCommand} timed out after {timeout.TotalMinutes:0.##} minutes; the process was stopped.";
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = (stdout + Environment.NewLine + stderr).Trim();
        return $"Command: {displayCommand}\nExit code: {process.ExitCode}\n" +
               (String.IsNullOrWhiteSpace(output) ? "(no output)" : output.Limit(Constants.MaxLength));
    }

    private static string? FirstExisting(string basePath, IEnumerable<string> extensions) =>
        extensions.Select(extension => basePath + extension).FirstOrDefault(File.Exists);
}
