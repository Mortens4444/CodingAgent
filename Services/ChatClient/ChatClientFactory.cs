using CodingAgent.Enums;
using CodingAgent.Extensions;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CodingAgent.Services.ChatClient;

internal static class ChatClientFactory
{
    public static IChatClient Get(string uri, ChatClientType chatClientType = ChatClientType.Qwen2_5_coder_14b, int timeoutInMinutes = 10)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(uri),
            Timeout = TimeSpan.FromMinutes(timeoutInMinutes)
        };
        return new OllamaApiClient(httpClient, chatClientType.GetDescription());
    }
}
