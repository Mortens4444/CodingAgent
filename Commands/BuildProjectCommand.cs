using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

public class BuildProjectCommand : Command, IToolCommand
{
    public Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        return codingLanguageService.BuildAsync();
    }
}
