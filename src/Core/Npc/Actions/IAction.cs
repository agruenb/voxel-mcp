namespace VoxelKing.Core.Npc.Actions;

/// <summary>
/// Interface for NPC actions executed by the behaviour arbiter or Lua brain host.
/// </summary>
public interface IAction
{
    /// <summary>
    /// Descriptive name of the action type.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Current execution status.
    /// </summary>
    ActionStatus Status { get; }

    /// <summary>
    /// Begins action execution, validating initial preconditions and initializing state.
    /// </summary>
    void Start();

    /// <summary>
    /// Advances action execution by dt seconds on the main simulation thread.
    /// </summary>
    ActionStatus Tick(float dt);

    /// <summary>
    /// Cancels action execution, transitioning status to Failed("interrupted").
    /// </summary>
    void Cancel();
}
