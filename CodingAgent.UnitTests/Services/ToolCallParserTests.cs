using CodingAgent.Services;
using System.Text.Json;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ToolCallParserTests
{
    [Test]
    public void TryParseAction_WhenNoJsonFound_ReturnsFalseAndSpecificError()
    {
        // Arrange
        string output = "Hello, this response contains no JSON block.";

        // Act
        bool success = ToolCallParser.TryParseAction(output, out var action, out var error);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(error, Is.EqualTo("The model's response does not contain a JSON object."));
    }

    [Test]
    public void TryParseAction_WhenInvalidJson_ReturnsFalseAndJsonExceptionError()
    {
        // Arrange - Contains matching braces but malformed JSON syntax inside
        string output = "Here is broken JSON: { \"name\": }";

        // Act
        bool success = ToolCallParser.TryParseAction(output, out var action, out var error);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(error, Does.StartWith("Invalid JSON:"));
    }

    [Test]
    public void TryParseAction_WhenFunctionNameIsMissing_ReturnsFalseAndError()
    {
        // Arrange - Valid JSON object, but no tool/function name provided
        string output = "{\"arguments\": {\"path\": \".\"}}";

        // Act
        bool success = ToolCallParser.TryParseAction(output, out var action, out var error);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(error, Is.EqualTo("The function name is missing from the JSON."));
    }

    [TestCase("{\"name\": \"list_files\"}", "list_files")]
    [TestCase("{\"function_name\": \"search_text\"}", "search_text")]
    [TestCase("{\"tool_name\": \"read_file\"}", "read_file")]
    [TestCase("Some preamble text {\"name\": \"write_file\"} trailing text", "write_file")]
    [TestCase("{\"function\": \"build_project\"}", "build_project")]
    [TestCase("{\"function\": {\"name\": \"run_tests\"}}", "run_tests")]
    [TestCase("{\"tool_call\": {\"tool_name\": \"finish\"}}", "finish")]
    public void TryParseAction_VariousValidFormats_ParsesNameSuccessfully(string input, string expectedName)
    {
        // Act
        bool success = ToolCallParser.TryParseAction(input, out var action, out var error);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(error, Is.Empty);
        Assert.That(action.Name, Is.EqualTo(expectedName));
    }

    [Test]
    public void TryParseAction_WithArguments_ParsesArgumentsSuccessfully()
    {
        // Arrange
        string input = "{\"name\": \"list_files\", \"arguments\": {\"path\": \"./src\"}}";

        // Act
        bool success = ToolCallParser.TryParseAction(input, out var action, out var error);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(action.Arguments.TryGetProperty("path", out var pathElement), Is.True);
        Assert.That(pathElement.GetString(), Is.EqualTo("./src"));
    }

    [Test]
    public void TryParseAction_ArgumentsAsJsonString_AreParsed()
    {
        string input = "{\"name\": \"read_file\", \"arguments\": \"{\\\"path\\\": \\\"a.cs\\\"}\"}";

        bool success = ToolCallParser.TryParseAction(input, out var action, out _);

        Assert.That(success, Is.True);
        Assert.That(action.Arguments.GetProperty("path").GetString(), Is.EqualTo("a.cs"));
    }

    [Test]
    public void TryParseAction_WithoutArguments_DefaultsToEmptyObject()
    {
        // Arrange
        string input = "{\"name\": \"list_files\"}";

        // Act
        bool success = ToolCallParser.TryParseAction(input, out var action, out var error);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(action.Arguments.ValueKind, Is.EqualTo(JsonValueKind.Object));
    }

    [Test]
    public void TryParseAction_WhenTextFollowsTheObject_ParsesOnlyTheFirstObject()
    {
        string output = "{\"name\":\"read_file\",\"arguments\":{\"path\":\"a}b\\\"c.cs\"}}\n### Instruction:\n{\"name\":\"finish\"}";

        bool success = ToolCallParser.TryParseAction(output, out var action, out var error);

        Assert.That(success, Is.True, error);
        Assert.That(action.Name, Is.EqualTo("read_file"));
        Assert.That(action.Arguments.GetProperty("path").GetString(), Is.EqualTo("a}b\"c.cs"));
    }
}
