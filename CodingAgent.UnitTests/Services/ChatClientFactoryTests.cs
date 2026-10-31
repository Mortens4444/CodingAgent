using CodingAgent.Services;
using CodingAgent.Services.ChatClient;
using Microsoft.Extensions.AI;

namespace CodingAgent.UnitTests.Services;

[TestFixture]
public class ChatClientFactoryTests
{
    [Test]
    public void ConfigureOptions_SetsContextLengthAndToolSchema()
    {
        var options = new ChatOptions();

        ChatClientFactory.ConfigureOptions(options, "qwen2.5-coder:14b", 8192);

        Assert.That(options.AdditionalProperties!["num_ctx"], Is.EqualTo(8192));
        Assert.That(options.ResponseFormat, Is.SameAs(ToolCatalog.ResponseFormat));
        Assert.That(options.StopSequences, Is.Null);
    }

    [Test]
    public void ConfigureOptions_DeepseekCoder_AddsStopSequences()
    {
        var options = new ChatOptions();

        ChatClientFactory.ConfigureOptions(options, "deepseek-coder:6.7b", 8192);

        Assert.That(options.StopSequences, Does.Contain("### Instruction:"));
    }
}
