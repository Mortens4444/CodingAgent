using System.ComponentModel;

namespace CodingAgent.Enums;

// Known Ollama tags. New members go at the end so existing numeric values stay stable.
public enum ChatClientType
{
    [Description("qwen2.5-coder:7b")]
    Qwen2_5_coder_7b = 0,

    [Description("qwen2.5-coder:14b")]
    Qwen2_5_coder_14b = 1,

    // Same weights as qwen2.5-coder:32b-instruct-q4_K_M; this is the tag Ollama installs.
    [Description("qwen2.5-coder:32b")]
    Qwen2_5_coder_32b = 2,

    [Description("qwen3.5:9b")]
    Qwen3_5_9b = 3,

    [Description("deepseek-coder:6.7b")]
    Deepseek_coder_6_7b = 4,

    // Needs ~19 GB; on small GPUs it runs mostly on CPU and is very slow.
    [Description("deepseek-coder:33b")]
    Deepseek_coder_33b = 5
}
