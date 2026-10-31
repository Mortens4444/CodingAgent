using System.Text.Json;

namespace CodingAgent.Extensions;

public static class JsonElementExtensions
{
    public static string GetString(this JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    // The first of several alternative argument names that holds a string, or null when none does.
    public static string? GetFirstString(this JsonElement element, params string[] propertyNames) =>
        propertyNames.Select(name => element.GetString(name, null!)).FirstOrDefault(value => value is not null);
}
