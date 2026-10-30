using CodingAgent.Extensions;
using System.Diagnostics;

namespace CodingAgent.Services.LanguageServices;

public class LanguageService(string root)
{
    public string Root { get; } = root;

    protected static async Task<string> RunProcessAsync(string executable, string workingDirectory, IEnumerable<string> arguments, string displayCommand)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return $"ERROR: {displayCommand} timed out after 3 minutes; the process was stopped.";
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = (stdout + Environment.NewLine + stderr).Trim();
        return $"Command: {displayCommand}\nExit code: {process.ExitCode}\n" +
               (String.IsNullOrWhiteSpace(output) ? "(no output)" : output.Limit(Constants.MaxLength));
    }
}
