namespace VoxelKing.Core.Llm;

/// <summary>
/// Token consumption metrics reported by an LLM call or estimated locally.
/// </summary>
public readonly record struct LlmUsage(int PromptTokens, int OutputTokens, int TotalTokens)
{
    public static readonly LlmUsage Zero = new(0, 0, 0);

    public override string ToString() => $"Tokens: {PromptTokens} prompt + {OutputTokens} output = {TotalTokens} total";
}
