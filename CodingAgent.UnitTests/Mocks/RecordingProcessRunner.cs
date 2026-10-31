using Mocklis;
using System.ComponentModel;

namespace CodingAgent.UnitTests.Mocks;

// Wraps the Mocklis mock: records every started command and answers with a configurable result.
public sealed class RecordingProcessRunner
{
    public MockProcessRunner Mock { get; } = new();

    public List<string> Commands { get; } = [];

    public HashSet<string> Missing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Func<string, IReadOnlyList<string>, string> Result { get; set; } = (_, _) => Success("ok");

    public RecordingProcessRunner()
    {
        Mock.FindExecutable.Func(name => Missing.Contains(name) ? null : "/usr/bin/" + name);
        Mock.RunAsync.Func(call =>
        {
            var (executable, _, arguments, _) = call;
            if (Missing.Contains(executable))
                throw new Win32Exception(2, "not found");

            var name = Path.GetFileName(executable);
            Commands.Add(String.Join(" ", new[] { name }.Concat(arguments)));
            return Task.FromResult(Result(name, arguments));
        });
    }

    public static string Success(string output) => $"Command: x\nExit code: 0\n{output}";

    public static string Failure(string output) => $"Command: x\nExit code: 1\n{output}";
}
