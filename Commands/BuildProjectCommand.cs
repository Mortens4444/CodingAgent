using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

internal class BuildProjectCommand : Command, IToolCommand
{
    public Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        return codingLanguageService.BuildAsync();
    }
}
