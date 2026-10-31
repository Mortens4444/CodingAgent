using System.Text.Json;

namespace CodingAgent.Services;

public sealed record ParsedAction(string Name, JsonElement Arguments);

public static class ToolCallParser
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
        var end = start < 0 ? -1 : FindObjectEnd(output, start);

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

    // Returns the index of the brace closing the object that starts at 'start', so text after the
    // first object (for example a made-up next turn) is ignored. Falls back to the last '}'.
    private static int FindObjectEnd(string output, int start)
    {
        var depth = 0;
        var inString = false;

        for (var i = start; i < output.Length; i++)
        {
            var c = output[i];

            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }

        return output.LastIndexOf('}');
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
                if (!source.TryGetProperty(propertyName, out var candidate))
                    continue;

                if (candidate.ValueKind == JsonValueKind.Object)
                {
                    arguments = candidate.Clone();
                    return true;
                }

                // OpenAI-style calls encode the arguments as a JSON string: "arguments": "{\"path\":\".\"}".
                if (candidate.ValueKind == JsonValueKind.String && TryParseObject(candidate.GetString(), out arguments))
                    return true;
            }
        }

        arguments = default;
        return false;
    }

    private static bool TryParseObject(string? text, out JsonElement value)
    {
        value = default;
        if (String.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
            return false;

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            value = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private static JsonElement CreateEmptyObject() => EmptyObject;
}