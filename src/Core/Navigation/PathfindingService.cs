namespace VoxelKing.Core.Navigation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Time-sliced pathfinding service executing A* requests on the main thread within a per-frame budget.
/// </summary>
public sealed class PathfindingService : IPathfindingService
{
    private readonly Queue<PathRequest> _queue = new();
    private readonly Dictionary<long, PathRequest> _requestsById = new();
    private long _nextRequestId = 1;

    public IVoxelWorld World { get; }
    public IWalkability Walkability { get; }
    public int PendingRequestsCount => _queue.Count;

    public PathfindingService(IVoxelWorld world, IWalkability? walkability = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Walkability = walkability ?? new Walkability(world);
    }

    /// <inheritdoc />
    public PathResult FindPath(Vec3i start, Vec3i goal, PathfindingOptions? options = null)
    {
        return AStarPathfinder.FindPath(Walkability, start, goal, options);
    }

    /// <inheritdoc />
    public void RequestPath(PathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _queue.Enqueue(request);
        _requestsById[request.RequestId] = request;
    }

    /// <inheritdoc />
    public Task<PathResult> RequestPathAsync(
        Vec3i start,
        Vec3i goal,
        PathfindingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        long id = Interlocked.Increment(ref _nextRequestId);
        var request = new PathRequest(id, start, goal, options, cancellationToken);
        RequestPath(request);
        return request.CompletionSource.Task;
    }

    /// <inheritdoc />
    public bool CancelRequest(long requestId)
    {
        if (_requestsById.Remove(requestId, out var request))
        {
            request.CompletionSource.TrySetResult(PathResult.Failed(PathFailureReason.Cancelled));
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Tick(float maxMilliseconds = 2.0f, int maxNodesPerFrame = 5000)
    {
        if (_queue.Count == 0)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        int nodesProcessed = 0;

        while (_queue.Count > 0 && stopwatch.Elapsed.TotalMilliseconds < maxMilliseconds && nodesProcessed < maxNodesPerFrame)
        {
            var request = _queue.Dequeue();
            _requestsById.Remove(request.RequestId);

            if (request.IsCancelled)
            {
                request.CompletionSource.TrySetResult(PathResult.Failed(PathFailureReason.Cancelled));
                continue;
            }

            var result = AStarPathfinder.FindPath(Walkability, request.Start, request.Goal, request.Options);
            nodesProcessed += result.NodesExpanded;
            request.CompletionSource.TrySetResult(result);
        }
    }
}
