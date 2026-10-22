namespace VoxelKing.Core.Brain;

/// <summary>
/// Lifecycle and execution states of an NPC Lua brain host (ARCHITECTURE §5.4).
/// </summary>
public enum BrainStatus
{
    /// <summary>
    /// No script has been assigned to this brain host yet.
    /// </summary>
    Uninitialized = 0,

    /// <summary>
    /// The brain is actively executing its main coroutine, running an action, or waiting on a timer.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The brain completed its task normally (main returned or npc.done was called).
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The brain encountered a syntax error, runtime error, or budget violation and was aborted.
    /// </summary>
    Faulted = 3
}
