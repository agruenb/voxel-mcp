namespace VoxelKing.Core.Navigation;

using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Service managing time-sliced pathfinding requests on the main simulation thread.
/// </summary>
public interface IPathfindingService
{
    IVoxelWorld World { get; }
    IWalkability Walkability { get; }
    int PendingRequestsCount { get; }

    /// <summary>
    /// Executes an immediate synchronous A* path search.
    /// </summary>
    PathResult FindPath(Vec3i start, Vec3i goal, PathfindingOptions? options = null);

    /// <summary>
    /// Enqueues a path request to be processed during time-sliced frames.
    /// </summary>
    void RequestPath(PathRequest request);

    /// <summary>
    /// Enqueues a path request and returns an asynchronous task for completion.
    /// </summary>
    Task<PathResult> RequestPathAsync(
        Vec3i start,
        Vec3i goal,
        PathfindingOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drains queued pathfinding requests up to the frame time budget.
    /// </summary>
    void Tick(float maxMilliseconds = 2.0f, int maxNodesPerFrame = 5000);

    /// <summary>
    /// Cancels a pending path request by its ID.
    /// </summary>
    bool CancelRequest(long requestId);
}
