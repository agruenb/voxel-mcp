namespace VoxelKing.Core.Npc.Arbiter;

/// <summary>
/// Interface for the Task layer of the behaviour arbiter, driven by the Lua BrainHost (F-303).
/// </summary>
public interface ITaskLayer
{
    /// <summary>
    /// Whether the task layer currently has an active task/coroutine running.
    /// </summary>
    bool HasActiveTask { get; }

    /// <summary>
    /// Advances task simulation for dt seconds.
    /// </summary>
    void Tick(float dt);

    /// <summary>
    /// Called when the task layer is pre-empted by the reflex layer.
    /// Interrupts the currently executing task/action.
    /// </summary>
    void Interrupt();
}
