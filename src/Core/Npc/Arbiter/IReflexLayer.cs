namespace VoxelKing.Core.Npc.Arbiter;

/// <summary>
/// Interface for the highest-priority reflex layer handling immediate safety, unstuck, and hazard avoidance.
/// </summary>
public interface IReflexLayer
{
    /// <summary>
    /// Whether a reflex condition is currently active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Descriptive name of the current reflex trigger ("suffocation", "water", "drop_hazard", "stuck"), or null if inactive.
    /// </summary>
    string? CurrentReflexReason { get; }

    /// <summary>
    /// Evaluates reflex conditions (suffocation, water, drop &gt; 3, stuck) and executes corrective actions.
    /// Returns true if a reflex was triggered or is active.
    /// </summary>
    bool CheckReflex(float dt);

    /// <summary>
    /// Resets reflex state and timers.
    /// </summary>
    void Reset();
}
