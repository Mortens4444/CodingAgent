using System.Text.Json;

namespace CodingAgent.Extensions;

internal static class JsonElementExtensions
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
}
