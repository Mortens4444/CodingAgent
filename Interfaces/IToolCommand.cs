using System.Text.Json;

namespace CodingAgent.Interfaces;

public interface IToolCommand
{
    Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService);
}
