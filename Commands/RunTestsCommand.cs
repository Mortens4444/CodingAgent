using CodingAgent.Interfaces;
using System.Text.Json;

namespace CodingAgent.Commands;

internal class RunTestsCommand : Command, IToolCommand
{
    public Task<string> ExecuteAsync(JsonElement args, ICodingLanguageService codingLanguageService)
    {
        return codingLanguageService.RunTestsAsync();
    }
}
