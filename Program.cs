using CodingAgent.Enums;
using CodingAgent.Interfaces;
using CodingAgent.Services;
using CodingAgent.Services.ChatClient;
using CodingAgent.Services.LanguageServices;
using CodingAgent.Services.ToDo;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole();

        builder.Services.AddChatClient(sp =>
            ChatClientFactory.Get("http://localhost:11434", ChatClientType.Qwen2_5_coder_14b)
        )
        .UseLogging();

        builder.Services.AddTransient<CSharpCodingLanguageService>();
        builder.Services.AddTransient<PythonCodingLanguagetService>();

        //builder.Services.AddTransient<AskUserForTask>();
        builder.Services.AddTransient<IToDoProvider, ToDoFileReader>();
        builder.Services.AddTransient<ToolDispatcher>();
        builder.Services.AddTransient<ProjectContextLoader>();
        builder.Services.AddTransient<CodingAgentLoop>();
        builder.Services.AddTransient<CodingAgentApplication>();

        using var host = builder.Build();
        await host.Services.GetRequiredService<CodingAgentApplication>().RunAsync();
    }
}
