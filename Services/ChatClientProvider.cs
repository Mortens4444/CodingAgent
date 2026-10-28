using CodingAgent.Interfaces;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CodingAgent.Services;

internal class ChatClientProvider : IChatClientProvider
{
    public IChatClient Get(string uri, string model, int timeoutInMinutes = 10)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(uri),
            Timeout = TimeSpan.FromMinutes(timeoutInMinutes)
        };
        return new OllamaApiClient(httpClient, model);
    }
}
