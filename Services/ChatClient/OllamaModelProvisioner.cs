using OllamaSharp;

namespace CodingAgent.Services.ChatClient;

public static class OllamaModelProvisioner
{
    // Makes sure the model is installed on the Ollama server, pulling it when missing.
    // Pulled weights are stored where the server's OLLAMA_MODELS setting points.
    public static async Task<bool> EnsureModelAsync(IOllamaApiClient client, string model, CancellationToken cancellationToken = default)
    {
        try
        {
            var localModels = await client.ListLocalModelsAsync(cancellationToken);
            if (localModels.Any(m => IsSameTag(m.Name, model)))
                return true;

            ConsoleOutput.WriteLine($"Model '{model}' is not installed on the Ollama server. Pulling it...", ConsoleOutput.Warning);

            var lastStatus = String.Empty;
            await foreach (var progress in client.PullModelAsync(model, cancellationToken))
            {
                if (progress is null) continue;

                var status = progress.Total > 0 ? $"{progress.Status} {progress.Percent:0}%" : progress.Status;
                if (status != lastStatus)
                {
                    ConsoleOutput.WriteLine($"  {status}", ConsoleOutput.Tool);
                    lastStatus = status;
                }
            }

            localModels = await client.ListLocalModelsAsync(cancellationToken);
            if (localModels.Any(m => IsSameTag(m.Name, model)))
                return true;

            ConsoleOutput.WriteLine($"Model '{model}' is still not available after the pull.", ConsoleOutput.Error);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            ConsoleOutput.WriteLine($"Could not prepare model '{model}' on Ollama: {ex.Message}", ConsoleOutput.Error);
            return false;
        }
    }

    private static bool IsSameTag(string installed, string requested) =>
        String.Equals(WithTag(installed), WithTag(requested), StringComparison.OrdinalIgnoreCase);

    private static string WithTag(string name) => name.Contains(':') ? name : name + ":latest";
}
