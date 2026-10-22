namespace VoxelKing.Core.Navigation;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;

/// <summary>
/// A* pathfinder navigating discrete 3D voxel cells with node budget limits, range clamping, and partial path support.
/// </summary>
public static class AStarPathfinder
{
    private static readonly PathfindingOptions DefaultOptions = new();

    /// <summary>
    /// Searches for a navigable path from start to goal over the given walkability world.
    /// </summary>
    public static PathResult FindPath(
        IWalkability walkability,
        Vec3i start,
        Vec3i goal,
        PathfindingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(walkability);
        options ??= DefaultOptions;

        // Verify starting position is a valid walkable cell
        if (!walkability.IsWalkable(start))
        {
            return PathResult.Failed(PathFailureReason.StartNotWalkable);
        }

        // Trivial destination reached
        if (start == goal)
        {
            return PathResult.Succeeded(new[] { start }, 0, 0f);
        }

        float directDistance = Heuristic(start, goal);
        if (directDistance > options.MaxRange && !options.AllowPartial)
        {
            return PathResult.Failed(PathFailureReason.GoalOutOfRange);
        }

        var openSet = new PriorityQueue<Vec3i, float>();
        var gScore = new Dictionary<Vec3i, float>();
        var cameFrom = new Dictionary<Vec3i, Vec3i>();
        var closedSet = new HashSet<Vec3i>();

        gScore[start] = 0f;
        openSet.Enqueue(start, directDistance);

        Vec3i closestNode = start;
        float closestDistance = directDistance;
        int nodesExpanded = 0;

        while (openSet.Count > 0 && nodesExpanded < options.NodeBudget)
        {
            var current = openSet.Dequeue();

            if (!closedSet.Add(current))
            {
                continue;
            }

            nodesExpanded++;
            float currentG = gScore[current];

            if (current == goal)
            {
                var fullPath = ReconstructPath(cameFrom, start, goal);
                return PathResult.Succeeded(fullPath, nodesExpanded, currentG);
            }

            float currentH = Heuristic(current, goal);
            if (currentH < closestDistance)
            {
                closestDistance = currentH;
                closestNode = current;
            }

            foreach (var neighbor in walkability.NeighboursWithCost(current))
            {
                var dest = neighbor.Destination;

                if (closedSet.Contains(dest))
                {
                    continue;
                }

                if (start.Distance(dest) > options.MaxRange)
                {
                    continue;
                }

                float tentativeG = currentG + neighbor.Cost;

                if (!gScore.TryGetValue(dest, out float existingG) || tentativeG < existingG)
                {
                    gScore[dest] = tentativeG;
                    cameFrom[dest] = current;
                    float h = Heuristic(dest, goal);
                    openSet.Enqueue(dest, tentativeG + h);

                    if (h < closestDistance)
                    {
                        closestDistance = h;
                        closestNode = dest;
                    }
                }
            }
        }

        bool budgetExceeded = nodesExpanded >= options.NodeBudget;
        var failureReason = budgetExceeded ? PathFailureReason.BudgetExceeded : PathFailureReason.NoPathFound;

        if (options.AllowPartial && closestNode != start)
        {
            var partialPath = ReconstructPath(cameFrom, start, closestNode);
            return PathResult.Partial(partialPath, nodesExpanded, gScore[closestNode], failureReason);
        }

        return PathResult.Failed(failureReason, nodesExpanded);
    }

    private static IReadOnlyList<Vec3i> ReconstructPath(
        Dictionary<Vec3i, Vec3i> cameFrom,
        Vec3i start,
        Vec3i end)
    {
        var path = new List<Vec3i>();
        var current = end;

        while (current != start)
        {
            path.Add(current);
            if (!cameFrom.TryGetValue(current, out current))
            {
                break;
            }
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

    private static float Heuristic(Vec3i a, Vec3i b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
