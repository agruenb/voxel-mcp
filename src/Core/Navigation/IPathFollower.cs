namespace VoxelKing.Core.Navigation;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;

/// <summary>
/// Controls an NPC's movement along a sequence of waypoints with stuck detection and world invalidation.
/// </summary>
public interface IPathFollower : IDisposable
{
    NpcState Npc { get; }
    IReadOnlyList<Vec3i> CurrentPath { get; }
    IReadOnlyList<Vec3i> RemainingWaypoints { get; }
    int CurrentWaypointIndex { get; }
    Vec3i? TargetGoal { get; }
    PathFollowerStatus Status { get; }
    float MoveSpeed { get; set; }
    float StuckTimeout { get; set; }

    /// <summary>
    /// Starts following a new sequence of path waypoints.
    /// </summary>
    void FollowPath(IReadOnlyList<Vec3i> path, Vec3i? goal = null);

    /// <summary>
    /// Updates steering, arrival checks, and stuck detection for the current tick.
    /// </summary>
    void Update(float dt);

    /// <summary>
    /// Stops path following and resets velocity.
    /// </summary>
    void Stop();
}
