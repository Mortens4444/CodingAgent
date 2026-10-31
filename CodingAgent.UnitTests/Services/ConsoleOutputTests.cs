using CodingAgent.Services;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ConsoleOutputTests
{
    private TextWriter _originalOut = null!;
    private StringWriter _out = null!;
    private bool _originalEnabled;

    [SetUp]
    public void Setup()
    {
        _originalOut = Console.Out;
        _originalEnabled = ConsoleOutput.Enabled;
        _out = new StringWriter();
        Console.SetOut(_out);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        ConsoleOutput.Enabled = _originalEnabled;
        _out.Dispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WriteLine_WritesTextWithOrWithoutColor(bool enabled)
    {
        ConsoleOutput.Enabled = enabled;

        ConsoleOutput.WriteLine("hello", ConsoleOutput.Success);
        ConsoleOutput.Write("more", ConsoleOutput.Error);

        Assert.That(_out.ToString(), Is.EqualTo("hello" + Environment.NewLine + "more"));
    }

    [TestCase("ERROR: File not found", ConsoleColor.Red)]
    [TestCase("DENIED: The user did not allow 'npm'", ConsoleColor.Yellow)]
    [TestCase("EDIT SUCCESS: a.cs: replaced 1 occurrence(s)", ConsoleColor.Green)]
    [TestCase("Command: dotnet build\nExit code: 0", ConsoleColor.Green)]
    [TestCase("Command: dotnet build\nExit code: 1", ConsoleColor.Red)]
    [TestCase("src/\nProgram.cs", ConsoleColor.DarkGray)]
    public void ForToolResult_PicksColorByOutcome(string result, ConsoleColor expected)
    {
        Assert.That(ConsoleOutput.ForToolResult(result), Is.EqualTo(expected));
    }
}
