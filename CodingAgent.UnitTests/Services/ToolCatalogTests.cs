using CodingAgent.Services;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ToolCatalogTests
{
    [Test]
    public void SupportedToolsText_ReturnsCorrectCommaSeparatedList()
    {
        // Arrange & Act
        string result = ToolCatalog.SupportedToolsText;

        // Assert
        string expected = "list_files, search_text, read_file, write_file, check_for_missing_files, build_project, run_tests, finish";
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("list_files", true)]
    [TestCase("SEARCH_TEXT", true)] // Case-insensitive check
    [TestCase("read_file", true)]
    [TestCase("finish", true)]
    [TestCase("unknown_tool", false)]
    [TestCase("", false)]
    [TestCase(null!, false)] // Handled gracefully or returns false
    public void IsValid_ReturnsExpectedResult(string toolName, bool expectedIsValid)
    {
        // Act
        bool result = ToolCatalog.IsValid(toolName);

        // Assert
        Assert.That(result, Is.EqualTo(expectedIsValid));
    }

    [TestCase("list_directory", "list_files")]
    [TestCase("LIST_DIR", "list_files")]
    [TestCase("search_files", "search_text")]
    [TestCase("get_file_contents", "read_file")]
    [TestCase("edit_file", "write_file")]
    [TestCase("update_file", "write_file")]
    [TestCase("check_file_exists", "check_for_missing_files")]
    [TestCase("check_missing_files", "check_for_missing_files")]
    [TestCase("build_solution", "build_project")]
    [TestCase("test_project", "run_tests")]
    [TestCase("complete", "finish")]
    [TestCase("  list_files  ", "list_files")] // Trimming whitespace
    [TestCase("custom_unknown_tool", "custom_unknown_tool")] // Fallback to trimmed input
    public void NormalizeToolName_ReturnsExpectedNormalizedName(string input, string expected)
    {
        // Act
        string result = ToolCatalog.NormalizeToolName(input);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }
}