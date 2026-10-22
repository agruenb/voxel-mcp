namespace VoxelKing.Core.Llm;

/// <summary>
/// Status outcome of a brain compilation operation (F-404, LLM_INTEGRATION §3).
/// </summary>
public enum CompileStatus
{
    /// <summary>
    /// LLM generated a valid script that passed validation and was swapped into the NPC's brain host.
    /// </summary>
    Success,

    /// <summary>
    /// LLM requested clarification from the player; no new script generated, current brain continues.
    /// </summary>
    ClarificationRequested,

    /// <summary>
    /// Player instruction was chit-chat or casual conversation; no new script generated, current brain continues.
    /// </summary>
    KeepCurrentTask,

    /// <summary>
    /// Generated script failed validation even after all repair retries; current brain continues.
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// Generation failed due to an LLM provider, network, timeout, or quota error.
    /// </summary>
    Error,

    /// <summary>
    /// Compilation request was cancelled (e.g. superseded by a new player instruction or cancelled by caller).
    /// </summary>
    Cancelled
}
