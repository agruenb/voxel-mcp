namespace VoxelKing.Core.Llm;

using System;

/// <summary>
/// Represents a message turn in an LLM conversation.
/// </summary>
public sealed record LlmMessage
{
    /// <summary>
    /// Role of the author: "user" or "model".
    /// </summary>
    public string Role { get; init; }

    /// <summary>
    /// Text content of the message.
    /// </summary>
    public string Text { get; init; }

    public LlmMessage(string role, string text)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>
    /// Creates a user message turn.
    /// </summary>
    public static LlmMessage User(string text) => new("user", text);

    /// <summary>
    /// Creates a model response message turn.
    /// </summary>
    public static LlmMessage Model(string text) => new("model", text);

    public override string ToString() => $"[{Role}] {Text}";
}
