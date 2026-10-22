namespace VoxelKing.Core.Navigation;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;

/// <summary>
/// Status of an A* pathfinding query.
/// </summary>
public enum PathStatus
{
    Complete,
    Partial,
    Failed
}

/// <summary>
/// Detailed reason for pathfinding failure or partial results.
/// </summary>
public enum PathFailureReason
{
    None,
    StartNotWalkable,
    GoalOutOfRange,
    BudgetExceeded,
    NoPathFound,
    Cancelled
}

/// <summary>
/// Result of an A* pathfinding search.
/// </summary>
public sealed record PathResult(
    PathStatus Status,
    IReadOnlyList<Vec3i> Waypoints,
    PathFailureReason FailureReason,
    int NodesExpanded,
    float TotalCost)
{
    public bool Success => Status == PathStatus.Complete;
    public bool HasPath => Waypoints.Count > 0;

    public static PathResult Succeeded(IReadOnlyList<Vec3i> waypoints, int nodesExpanded, float cost) =>
        new(PathStatus.Complete, waypoints, PathFailureReason.None, nodesExpanded, cost);

    public static PathResult Partial(IReadOnlyList<Vec3i> waypoints, int nodesExpanded, float cost, PathFailureReason reason = PathFailureReason.NoPathFound) =>
        new(PathStatus.Partial, waypoints, reason, nodesExpanded, cost);

    public static PathResult Failed(PathFailureReason reason, int nodesExpanded = 0) =>
        new(PathStatus.Failed, Array.Empty<Vec3i>(), reason, nodesExpanded, 0f);
}
