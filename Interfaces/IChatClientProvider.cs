using Microsoft.Extensions.AI;

namespace CodingAgent.Interfaces;

internal interface IChatClientProvider
{
    IChatClient Get(string uri, string model, int timeoutInMinutes = 10);
}
