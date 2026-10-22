namespace VoxelKing.Core.Llm;

using System;

/// <summary>
/// Represents a single speaker turn in an interactive dialogue conversation between an NPC and player.
/// </summary>
public sealed record ConversationTurn
{
    /// <summary>
    /// Gets the name or role of the speaker (e.g. "Player" or the NPC's name).
    /// </summary>
    public string Speaker { get; init; }

    /// <summary>
    /// Gets the spoken message text.
    /// </summary>
    public string Message { get; init; }

    public ConversationTurn(string speaker, string message)
    {
        Speaker = speaker ?? throw new ArgumentNullException(nameof(speaker));
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public override string ToString() => $"{Speaker}: \"{Message}\"";
}
