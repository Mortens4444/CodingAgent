using System.Text.Json;

namespace CodingAgent.Interfaces;

internal interface IToolCommand
{
    Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService);
}
