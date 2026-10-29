using CodingAgent.Enums;
using CodingAgent.Extensions;

namespace CodingAgent.Services.ChatClient;

internal static class ChatClientFactory
{
    public static string GetChatClientModelName(ChatClientType chatClientType)
    {
        return chatClientType.GetDescription();
    }
}
