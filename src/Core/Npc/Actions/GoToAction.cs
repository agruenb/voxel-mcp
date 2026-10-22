namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;

/// <summary>
/// Configurable options for the GoTo action.
/// </summary>
public sealed record GoToOptions
{
    /// <summary>
    /// Arrival tolerance distance in blocks. Default 0 (exact cell); use >= 1 for targets inside solid blocks.
    /// </summary>
    public float Tolerance { get; init; } = 0f;

    /// <summary>
    /// Timeout in seconds before action fails with "timeout". Default 30.
    /// </summary>
    public float Timeout { get; init; } = 30f;

    /// <summary>
    /// Whether navigating to a partial path succeeds or fails with "unreachable" upon reaching the closest node.
    /// </summary>
    public bool AllowPartial { get; init; } = false;

    /// <summary>
    /// Horizontal movement speed in meters/second. Default 3.5.
    /// </summary>
    public float MoveSpeed { get; init; } = 3.5f;
}

/// <summary>
/// Action navigating an NPC to a target block coordinate using voxel pathfinding and path following.
/// </summary>
public class GoToAction : ActionBase
{
    public override string Name => "GoTo";

    public NpcState Npc { get; }
    public Vec3i TargetPos { get; }
    public GoToOptions Options { get; }
    public IPathfindingService? PathfindingService { get; }
    public IVoxelWorld? World { get; }
    public IEventBus? EventBus { get; }
    public IWalkability? Walkability { get; }

    private PathFollower? _follower;
    private bool _isPartial;

    public GoToAction(
        NpcState npc,
        Vec3i targetPos,
        GoToOptions? options = null,
        IPathfindingService? pathfindingService = null,
        IVoxelWorld? world = null,
        IEventBus? eventBus = null,
        IWalkability? walkability = null)
        : base(options?.Timeout ?? 30f)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        TargetPos = targetPos;
        Options = options ?? new GoToOptions();
        PathfindingService = pathfindingService;
        World = world;
        EventBus = eventBus;
        Walkability = walkability;
    }

    protected override void OnStart()
    {
        var targetCenter = new Vec3(TargetPos.X + 0.5f, TargetPos.Y, TargetPos.Z + 0.5f);

        // Precondition: check if already at target position within tolerance
        if (Options.Tolerance > 0f)
        {
            if (Npc.Position.Distance(targetCenter) <= Options.Tolerance)
            {
                Succeed();
                return;
            }
        }
        else
        {
            var npcBlock = Npc.Position.FloorToInt();
            if (npcBlock == TargetPos)
            {
                Succeed();
                return;
            }
        }

        if (PathfindingService == null || World == null)
        {
            Fail(ActionFailureReason.Unreachable);
            return;
        }

        var walk = Walkability ?? PathfindingService.Walkability;
        var startCell = Npc.Position.FloorToInt();

        // Adjust starting cell if current voxel is not walkable
        if (!walk.IsWalkable(startCell))
        {
            var surface = walk.Surface(startCell.X, startCell.Z, startCell.Y + 2, startCell.Y - 2);
            if (surface.HasValue)
            {
                startCell = surface.Value;
            }
        }

        var goalCell = TargetPos;
        if (!walk.IsWalkable(goalCell) && Options.Tolerance > 0f)
        {
            var surface = walk.Surface(goalCell.X, goalCell.Z, goalCell.Y + 2, goalCell.Y - 2);
            if (surface.HasValue)
            {
                goalCell = surface.Value;
            }
        }

        var pOptions = new PathfindingOptions
        {
            AllowPartial = true
        };

        var result = PathfindingService.FindPath(startCell, goalCell, pOptions);

        if (!result.HasPath || result.Status == PathStatus.Failed)
        {
            Fail(ActionFailureReason.Unreachable);
            return;
        }

        if (result.Status == PathStatus.Partial)
        {
            _isPartial = true;
            // If partial path doesn't even move towards goal, fail immediately
            if (result.Waypoints.Count <= 1 && !Options.AllowPartial)
            {
                Fail(ActionFailureReason.Unreachable);
                return;
            }
        }

        _follower = new PathFollower(Npc, World, EventBus, walk)
        {
            MoveSpeed = Options.MoveSpeed
        };
        Npc.PathFollower = _follower;
        _follower.FollowPath(result.Waypoints, goalCell);
    }

    protected override void OnTick(float dt)
    {
        var targetCenter = new Vec3(TargetPos.X + 0.5f, TargetPos.Y, TargetPos.Z + 0.5f);
        float arrivalDist = Options.Tolerance > 0f ? Options.Tolerance : 0.5f;
        if (Npc.Position.Distance(targetCenter) <= arrivalDist || Npc.Position.FloorToInt() == TargetPos)
        {
            CleanUpFollower();
            Succeed();
            return;
        }

        if (_follower == null)
        {
            return;
        }

        _follower.Update(dt);

        switch (_follower.Status)
        {
            case PathFollowerStatus.Arrived:
                CleanUpFollower();
                if (_isPartial && !Options.AllowPartial)
                {
                    Fail(ActionFailureReason.Unreachable);
                }
                else
                {
                    Succeed();
                }
                break;

            case PathFollowerStatus.Stuck:
                CleanUpFollower();
                Fail(ActionFailureReason.Stuck);
                break;

            case PathFollowerStatus.Invalidated:
                CleanUpFollower();
                Fail(ActionFailureReason.Unreachable);
                break;
        }
    }

    protected override void OnCancel()
    {
        CleanUpFollower();
    }

    protected override void OnTimeout()
    {
        CleanUpFollower();
    }

    private void CleanUpFollower()
    {
        _follower?.Stop();
        if (ReferenceEquals(Npc.PathFollower, _follower))
        {
            Npc.PathFollower = null;
        }
        _follower = null;
    }
}
