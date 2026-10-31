using CodingAgent.Enums;
using CodingAgent.Extensions;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CodingAgent.Services.ChatClient;

public static class ChatClientFactory
{
    public static readonly string DefaultModel = ChatClientType.Qwen2_5_coder_14b.GetDescription();

    public static OllamaApiClient Get(string uri, ChatClientType chatClientType, int timeoutInMinutes = 30) =>
        Get(uri, chatClientType.GetDescription(), timeoutInMinutes);

    public static OllamaApiClient Get(string uri, string model, int timeoutInMinutes = 30)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(uri),
            Timeout = timeoutInMinutes > 0 ? TimeSpan.FromMinutes(timeoutInMinutes) : Timeout.InfiniteTimeSpan
        };
        return new OllamaApiClient(httpClient, model);
    }

    public static void ConfigureOptions(ChatOptions options, string model, int contextLength)
    {
        options.AdditionalProperties ??= [];
        options.AdditionalProperties["num_ctx"] = contextLength;

        // Constrains sampling to {"name": <supported tool>, "arguments": {...}}, so the model cannot invent tool names.
        options.ResponseFormat = ToolCatalog.ResponseFormat;

        // deepseek-coder's Ollama template has no stop words; without them a reply can run into a made-up next turn.
        if (model.StartsWith("deepseek-coder", StringComparison.OrdinalIgnoreCase))
            options.StopSequences = ["<|EOT|>", "### Instruction:"];
    }
}
