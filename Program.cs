using CodingAgent;
using CodingAgent.Enums;
using CodingAgent.Extensions;
using CodingAgent.Interfaces;
using CodingAgent.Services;
using CodingAgent.Services.ChatClient;
using CodingAgent.Services.LanguageServices;
using CodingAgent.Services.ToDo;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

// Composition root; covered by running the app rather than unit tests.
[ExcludeFromCodeCoverage]
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole();

        // Override with --CodingAgent:Model=<tag or ChatClientType name>, the CodingAgent__Model environment variable, or appsettings.json.
        var model = ResolveModel(builder.Configuration["CodingAgent:Model"]);
        var ollamaUrl = builder.Configuration.GetValue("CodingAgent:OllamaUrl", "http://localhost:11434")!;
        var contextLength = builder.Configuration.GetValue("CodingAgent:ContextLength", Constants.DefaultContextLength);
        var timeoutInMinutes = builder.Configuration.GetValue("CodingAgent:TimeoutMinutes", 30);

        var ollamaClient = ChatClientFactory.Get(ollamaUrl, model, timeoutInMinutes);

        Console.WriteLine($"Model: {model} (context: {contextLength} tokens)");
        if (!await OllamaModelProvisioner.EnsureModelAsync(ollamaClient, model))
            return;

        builder.Services.AddChatClient(ollamaClient)
            .ConfigureOptions(options => ChatClientFactory.ConfigureOptions(options, model, contextLength))
            .UseLogging();

        builder.Services.AddSingleton<IProcessRunner>(ProcessRunner.Default);

        //builder.Services.AddTransient<AskUserForTask>();
        builder.Services.AddTransient<IToDoProvider, ToDoFileReader>();
        builder.Services.AddTransient<ToolDispatcher>();
        builder.Services.AddTransient<ProjectContextLoader>();
        builder.Services.AddTransient(sp => new CodingAgentLoop(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ToolDispatcher>(),
            contextLength));
        builder.Services.AddTransient<CodingAgentApplication>();

        using var host = builder.Build();
        await host.Services.GetRequiredService<CodingAgentApplication>().RunAsync();
    }

    private static string ResolveModel(string? configured)
    {
        if (String.IsNullOrWhiteSpace(configured))
            return ChatClientFactory.DefaultModel;

        return Enum.TryParse<ChatClientType>(configured, ignoreCase: true, out var type) && !Int32.TryParse(configured, out _)
            ? type.GetDescription()
            : configured.Trim();
    }
}
