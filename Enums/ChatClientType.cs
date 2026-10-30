using System.ComponentModel;

namespace CodingAgent.Enums;

public enum ChatClientType
{
    [Description("qwen2.5-coder:7b")]
    Qwen2_5_coder_7b,

    [Description("qwen2.5-coder:14b")]
    Qwen2_5_coder_14b,

    [Description("qwen2.5-coder:32b-instruct-q4_K_M")]
    Qwen2_5_coder_32b_instruct_q4_K_M,

    [Description("qwen3.5:9b")]
    Qwen3_5_9b
}
