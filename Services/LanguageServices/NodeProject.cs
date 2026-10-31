using System.Text.Json;

namespace CodingAgent.Services.LanguageServices;

public static class NodeProject
{
    // npm init writes this placeholder test script; running it always fails.
    private const string PlaceholderTestScript = "echo \"Error: no test specified\" && exit 1";

    public static bool HasScript(string root, string scriptName)
    {
        var packageJson = Path.Combine(root, "package.json");
        if (!File.Exists(packageJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageJson));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("scripts", out var scripts) &&
                   scripts.ValueKind == JsonValueKind.Object &&
                   scripts.TryGetProperty(scriptName, out var script) &&
                   script.ValueKind == JsonValueKind.String &&
                   !String.IsNullOrWhiteSpace(script.GetString()) &&
                   script.GetString() != PlaceholderTestScript;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return false;
        }
    }

    public static bool HasLocalBinary(string root, string name) =>
        File.Exists(Path.Combine(root, "node_modules", ".bin", name)) ||
        File.Exists(Path.Combine(root, "node_modules", ".bin", name + ".cmd"));
}
