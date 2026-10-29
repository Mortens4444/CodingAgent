using System.ComponentModel;

namespace CodingAgent.Enums;

internal enum ChatClientType
{
    [Description("qwen2.5-coder:7b")]
    Qwen2_5_coder_7b,

    [Description("qwen2.5-coder:14b")]
    Qwen2_5_coder_14b,

    [Description("qwen3.5:9b")]
    Qwen3_5_9b
}
