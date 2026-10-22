namespace VoxelKing.Core.Llm;

using System.Collections.Generic;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Npc;

/// <summary>
/// Service interface for compiling LLM system instructions and dynamic user prompts (LLM_INTEGRATION §2).
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Gets the static, cacheable system instruction including hard rules, generated API reference,
    /// few-shot examples, and JSON schema requirements.
    /// </summary>
    string BuildSystemInstruction();

    /// <summary>
    /// Builds dynamic user content from an <see cref="NpcPromptContext"/> snapshot,
    /// trimming conversation turns and memory events if the token budget is exceeded.
    /// </summary>
    string BuildUserContent(NpcPromptContext context, PromptBuilderOptions? options = null);

    /// <summary>
    /// Builds dynamic user content from an active <see cref="NpcState"/> instance,
    /// trimming conversation turns and memory events if the token budget is exceeded.
    /// </summary>
    string BuildUserContent(
        NpcState npc,
        string instruction,
        string? instructor = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        PromptBuilderOptions? options = null);

    /// <summary>
    /// Builds a full prompt bundle (system instruction + user content + token count estimates)
    /// from an <see cref="NpcPromptContext"/> snapshot.
    /// </summary>
    BuiltPrompt BuildPrompt(NpcPromptContext context, PromptBuilderOptions? options = null);

    /// <summary>
    /// Builds a full prompt bundle (system instruction + user content + token count estimates)
    /// from an active <see cref="NpcState"/> instance.
    /// </summary>
    BuiltPrompt BuildPrompt(
        NpcState npc,
        string instruction,
        string? instructor = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        PromptBuilderOptions? options = null);

    /// <summary>
    /// Builds a follow-up repair turn message for validation retry loops (LLM_INTEGRATION §4).
    /// </summary>
    string BuildRepairContent(IReadOnlyList<ValidationError> errors, string? previousScript = null);
}
