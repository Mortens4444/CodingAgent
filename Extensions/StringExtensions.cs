namespace CodingAgent.Extensions;

public static class StringExtensions
{
    public static string Limit(this string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength] + "\n...[truncated]";
}
