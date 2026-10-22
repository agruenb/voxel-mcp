namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;

/// <summary>
/// Action following an entity within a target distance for a specified duration.
/// </summary>
public sealed class FollowAction : ActionBase
{
    public override string Name => "Follow";

    public NpcState Npc { get; }
    public EntityId TargetId { get; }
    public float Distance { get; }
    public float DurationSeconds { get; }
    public EntityRegistry Registry { get; }
    public IPathfindingService? PathfindingService { get; }
    public IVoxelWorld? World { get; }
    public IEventBus? EventBus { get; }
    public IWalkability? Walkability { get; }

    private PathFollower? _follower;
    private Vec3 _lastTargetPos;

    public FollowAction(
        NpcState npc,
        EntityId targetId,
        float dist,
        float seconds,
        EntityRegistry registry,
        IPathfindingService? pathfindingService = null,
        IVoxelWorld? world = null,
        IEventBus? eventBus = null,
        IWalkability? walkability = null)
        : base(0f)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        TargetId = targetId;
        Distance = dist;
        DurationSeconds = seconds;
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        PathfindingService = pathfindingService;
        World = world;
        EventBus = eventBus;
        Walkability = walkability;
    }

    protected override void OnStart()
    {
        if (TargetId == Npc.Id || Distance < 0f || DurationSeconds < 0f)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        if (!Registry.TryGet(TargetId, out var target) || (target is NpcState targetNpc && targetNpc.IsDead))
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        _lastTargetPos = target.Position;
    }

    protected override void OnTick(float dt)
    {
        if (DurationSeconds > 0f && ElapsedTime >= DurationSeconds)
        {
            CleanUpFollower();
            Succeed();
            return;
        }

        if (!Registry.TryGet(TargetId, out var target) || (target is NpcState targetNpc && targetNpc.IsDead))
        {
            CleanUpFollower();
            Fail(ActionFailureReason.TargetGone);
            return;
        }

        float currentDist = Npc.Position.Distance(target.Position);

        // Turn to face target
        var dirToTarget = target.Position - Npc.Position;
        if (dirToTarget.LengthSquared() > 1e-4f)
        {
            Npc.SetFacingDirection(dirToTarget);
        }

        if (currentDist <= Distance)
        {
            // Close enough; idle in place
            CleanUpFollower();
            Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);
            return;
        }

        // Target is further away than desired distance: move towards it
        if (PathfindingService == null || World == null)
        {
            CleanUpFollower();
            Fail(ActionFailureReason.Unreachable);
            return;
        }

        bool needsRepath = _follower == null ||
                           _follower.Status != PathFollowerStatus.Following ||
                           target.Position.Distance(_lastTargetPos) > 1.5f;

        if (needsRepath)
        {
            CleanUpFollower();
            _lastTargetPos = target.Position;

            var walk = Walkability ?? PathfindingService.Walkability;
            var startCell = Npc.Position.FloorToInt();
            var goalCell = target.Position.FloorToInt();

            if (!walk.IsWalkable(goalCell))
            {
                var surface = walk.Surface(goalCell.X, goalCell.Z, goalCell.Y + 2, goalCell.Y - 2);
                if (surface.HasValue)
                {
                    goalCell = surface.Value;
                }
            }

            var result = PathfindingService.FindPath(startCell, goalCell, new PathfindingOptions { AllowPartial = true });
            if (!result.HasPath)
            {
                Fail(ActionFailureReason.Unreachable);
                return;
            }

            _follower = new PathFollower(Npc, World, EventBus, walk);
            Npc.PathFollower = _follower;
            _follower.FollowPath(result.Waypoints, goalCell);
        }
        else if (_follower != null)
        {
            _follower.Update(dt);
            if (_follower.Status == PathFollowerStatus.Stuck)
            {
                CleanUpFollower();
                Fail(ActionFailureReason.Stuck);
                return;
            }

            if (_follower.Status == PathFollowerStatus.Invalidated)
            {
                CleanUpFollower();
                Fail(ActionFailureReason.Unreachable);
                return;
            }
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
