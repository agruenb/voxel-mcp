namespace VoxelKing.Core.Navigation;

using System;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;

/// <summary>
/// A queued pathfinding request processed in time-sliced slices.
/// </summary>
public sealed class PathRequest
{
    public long RequestId { get; }
    public Vec3i Start { get; }
    public Vec3i Goal { get; }
    public PathfindingOptions Options { get; }
    public TaskCompletionSource<PathResult> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken CancellationToken { get; }
    public bool IsCancelled => CancellationToken.IsCancellationRequested;

    public PathRequest(
        long requestId,
        Vec3i start,
        Vec3i goal,
        PathfindingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        RequestId = requestId;
        Start = start;
        Goal = goal;
        Options = options ?? new PathfindingOptions();
        CancellationToken = cancellationToken;
    }
}
