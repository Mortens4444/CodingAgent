using System.Text.Json;

namespace CodingAgent.Services;

internal sealed record ParsedAction(string Name, JsonElement Arguments);

internal static class ToolCallParser
{
    private static readonly string[] ArgumentNames =
    {
        "arguments", "parameters", "args", "params"
    };

    public static bool TryParseAction(
        string output,
        out ParsedAction action,
        out string error)
    {
        action = new ParsedAction(String.Empty, default);
        error = String.Empty;

        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            error = "The model's response does not contain a JSON object.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(output[start..(end + 1)]);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The model's JSON root is not an object.";
                return false;
            }

            root.TryGetProperty("function", out var functionElement);
            root.TryGetProperty("tool_call", out var toolCallElement);

            var name = GetFirstString(
                root,
                "name",
                "function_name",
                "tool_name",
                "tool",
                "tool_call");

            if (functionElement.ValueKind == JsonValueKind.String)
            {
                name ??= functionElement.GetString();
            }
            else if (functionElement.ValueKind == JsonValueKind.Object)
            {
                name ??= GetFirstString(
                    functionElement,
                    "name",
                    "function_name",
                    "tool_name",
                    "tool_call");
            }

            if (toolCallElement.ValueKind == JsonValueKind.Object)
            {
                name ??= GetFirstString(
                    toolCallElement,
                    "name",
                    "function_name",
                    "tool_name",
                    "tool_call");
            }

            if (String.IsNullOrWhiteSpace(name))
            {
                error = "The function name is missing from the JSON.";
                return false;
            }

            var arguments = TryGetArguments(
                root,
                functionElement,
                toolCallElement,
                out var parsedArguments)
                    ? parsedArguments
                    : CreateEmptyObject();

            action = new ParsedAction(name, arguments);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    private static string? GetFirstString(
        JsonElement element,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var result = value.GetString();

            if (!String.IsNullOrWhiteSpace(result))
                return result;
        }

        return null;
    }

    private static bool TryGetArguments(
        JsonElement root,
        JsonElement functionElement,
        JsonElement toolCallElement,
        out JsonElement arguments)
    {
        foreach (var source in new[] { root, functionElement, toolCallElement })
        {
            if (source.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var propertyName in ArgumentNames)
            {
                if (source.TryGetProperty(propertyName, out var candidate) &&
                    candidate.ValueKind == JsonValueKind.Object)
                {
                    arguments = candidate.Clone();
                    return true;
                }
            }
        }

        arguments = default;
        return false;
    }

    private static JsonElement CreateEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}