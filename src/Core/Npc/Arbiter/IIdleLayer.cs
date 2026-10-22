namespace VoxelKing.Core.Npc.Arbiter;

using VoxelKing.Core.Common;

/// <summary>
/// States for the idle behaviour layer.
/// </summary>
public enum IdleState
{
    Waiting,
    Wandering,
    LookingAtPlayer
}

/// <summary>
/// Interface for the Idle layer controlling NPC wandering near home and player gaze when no task is active.
/// </summary>
public interface IIdleLayer
{
    /// <summary>
    /// Current state of the idle layer.
    /// </summary>
    IdleState State { get; }

    /// <summary>
    /// Target block position of the active wander action, or null if not wandering.
    /// </summary>
    Vec3i? CurrentWanderGoal { get; }

    /// <summary>
    /// Maximum wander radius from the NPC's home coordinates in blocks (default 8).
    /// </summary>
    float WanderRadius { get; set; }

    /// <summary>
    /// Minimum pause time in seconds between wander trips.
    /// </summary>
    float MinWaitTime { get; set; }

    /// <summary>
    /// Maximum pause time in seconds between wander trips.
    /// </summary>
    float MaxWaitTime { get; set; }

    /// <summary>
    /// Radius in blocks within which nearby players are noticed and looked at.
    /// </summary>
    float PlayerNoticeRadius { get; set; }

    /// <summary>
    /// Advances idle simulation for dt seconds.
    /// </summary>
    void Tick(float dt);

    /// <summary>
    /// Resets idle state and active wander actions.
    /// </summary>
    void Reset();
}
