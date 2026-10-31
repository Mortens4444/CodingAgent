namespace CodingAgent.Services;

// Colors the agent's console output so model replies, tool results and host messages are easy to tell apart.
// Colors are left out when the output is redirected or the NO_COLOR environment variable is set (https://no-color.org).
public static class ConsoleOutput
{
    public const ConsoleColor Heading = ConsoleColor.Cyan;
    public const ConsoleColor Model = ConsoleColor.White;
    public const ConsoleColor Tool = ConsoleColor.DarkGray;
    public const ConsoleColor Success = ConsoleColor.Green;
    public const ConsoleColor Warning = ConsoleColor.Yellow;
    public const ConsoleColor Error = ConsoleColor.Red;

    private static readonly object gate = new();

    public static bool Enabled { get; set; } =
        !Console.IsOutputRedirected && String.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static void WriteLine(string? text, ConsoleColor color) => Write(text + Environment.NewLine, color);

    public static void Write(string? text, ConsoleColor color)
    {
        if (!Enabled)
        {
            Console.Write(text);
            return;
        }

        lock (gate)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }
    }

    // Errors and failed commands are red, denials yellow, edits and passing commands green, everything else dim.
    public static ConsoleColor ForToolResult(string result) =>
        result.StartsWith("ERROR", StringComparison.Ordinal) ? Error :
        result.StartsWith("DENIED", StringComparison.Ordinal) ? Warning :
        result.Contains(" SUCCESS", StringComparison.Ordinal) ? Success :
        result.Contains("Exit code:", StringComparison.Ordinal) ? (AgentProgress.Passed(result) ? Success : Error) :
        Tool;
}
