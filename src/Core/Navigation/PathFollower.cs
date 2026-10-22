namespace VoxelKing.Core.Navigation;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;

/// <summary>
/// Follows a planned path waypoint by waypoint, steering the NPC, jumping when stepping up,
/// detecting stuck conditions, and invalidating the path when world geometry changes along the route.
/// </summary>
public sealed class PathFollower : IPathFollower
{
    private IReadOnlyList<Vec3i> _path = Array.Empty<Vec3i>();
    private IDisposable? _blockSubscription;
    private float _stuckTimer;
    private Vec3 _lastProgressPos;

    public NpcState Npc { get; }
    public IVoxelWorld World { get; }
    public IWalkability Walkability { get; }

    public IReadOnlyList<Vec3i> CurrentPath => _path;
    public int CurrentWaypointIndex { get; private set; }
    public Vec3i? TargetGoal { get; private set; }
    public PathFollowerStatus Status { get; private set; } = PathFollowerStatus.Idle;

    public float MoveSpeed { get; set; } = 3.5f;
    public float ArrivalThreshold { get; set; } = 0.35f;
    public float StuckTimeout { get; set; } = 2.0f;
    public float ProgressThreshold { get; set; } = 0.15f;

    public IReadOnlyList<Vec3i> RemainingWaypoints
    {
        get
        {
            if (_path.Count == 0 || CurrentWaypointIndex >= _path.Count)
            {
                return Array.Empty<Vec3i>();
            }

            var remaining = new List<Vec3i>(_path.Count - CurrentWaypointIndex);
            for (int i = CurrentWaypointIndex; i < _path.Count; i++)
            {
                remaining.Add(_path[i]);
            }

            return remaining;
        }
    }

    public PathFollower(
        NpcState npc,
        IVoxelWorld world,
        IEventBus? eventBus = null,
        IWalkability? walkability = null)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Walkability = walkability ?? new Walkability(world);

        _lastProgressPos = npc.Position;

        if (eventBus != null)
        {
            _blockSubscription = eventBus.Subscribe<BlockChanged>(OnBlockChanged);
        }
    }

    /// <inheritdoc />
    public void FollowPath(IReadOnlyList<Vec3i> path, Vec3i? goal = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        _path = path;
        TargetGoal = goal ?? (path.Count > 0 ? path[^1] : null);
        _stuckTimer = 0f;
        _lastProgressPos = Npc.Position;

        if (path.Count == 0)
        {
            Status = PathFollowerStatus.Idle;
            CurrentWaypointIndex = 0;
            return;
        }

        // If NPC is already within proximity of path[0], skip to next waypoint if available
        var firstTarget = new Vec3(path[0].X + 0.5f, path[0].Y, path[0].Z + 0.5f);
        float distToFirst = MathF.Sqrt(
            MathF.Pow(firstTarget.X - Npc.Position.X, 2) +
            MathF.Pow(firstTarget.Z - Npc.Position.Z, 2));

        if (distToFirst <= ArrivalThreshold && path.Count > 1)
        {
            CurrentWaypointIndex = 1;
        }
        else
        {
            CurrentWaypointIndex = 0;
        }

        Status = PathFollowerStatus.Following;
    }

    /// <inheritdoc />
    public void Update(float dt)
    {
        if (Status != PathFollowerStatus.Following)
        {
            return;
        }

        if (CurrentWaypointIndex >= _path.Count)
        {
            CompleteArrival();
            return;
        }

        var wp = _path[CurrentWaypointIndex];
        var targetPos = new Vec3(wp.X + 0.5f, wp.Y, wp.Z + 0.5f);

        float dx = targetPos.X - Npc.Position.X;
        float dz = targetPos.Z - Npc.Position.Z;
        float hDist = MathF.Sqrt((dx * dx) + (dz * dz));
        float vDist = MathF.Abs(targetPos.Y - Npc.Position.Y);

        // Check if arrived at current waypoint
        if (hDist <= ArrivalThreshold && vDist <= 0.6f)
        {
            CurrentWaypointIndex++;
            _stuckTimer = 0f;
            _lastProgressPos = Npc.Position;

            if (CurrentWaypointIndex >= _path.Count)
            {
                CompleteArrival();
                return;
            }

            wp = _path[CurrentWaypointIndex];
            targetPos = new Vec3(wp.X + 0.5f, wp.Y, wp.Z + 0.5f);
            dx = targetPos.X - Npc.Position.X;
            dz = targetPos.Z - Npc.Position.Z;
            hDist = MathF.Sqrt((dx * dx) + (dz * dz));
        }

        // Steer toward current waypoint
        if (hDist > 1e-4f)
        {
            float dirX = dx / hDist;
            float dirZ = dz / hDist;
            Npc.Velocity = new Vec3(dirX * MoveSpeed, Npc.Velocity.Y, dirZ * MoveSpeed);
            Npc.SetFacingDirection(new Vec3(dirX, 0f, dirZ));

            // Jump if waypoint is higher and NPC is grounded
            float dy = targetPos.Y - Npc.Position.Y;
            if (dy > 0.3f && Npc.IsOnGround)
            {
                Npc.Jump();
            }
        }

        // Stuck detection
        float distMoved = Npc.Position.Distance(_lastProgressPos);
        if (distMoved >= ProgressThreshold)
        {
            _lastProgressPos = Npc.Position;
            _stuckTimer = 0f;
        }
        else
        {
            _stuckTimer += dt;
            if (_stuckTimer >= StuckTimeout)
            {
                Status = PathFollowerStatus.Stuck;
                Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        Status = PathFollowerStatus.Idle;
        _path = Array.Empty<Vec3i>();
        CurrentWaypointIndex = 0;
        Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);
    }

    private void CompleteArrival()
    {
        Status = PathFollowerStatus.Arrived;
        Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);
    }

    private void OnBlockChanged(BlockChanged e)
    {
        if (Status != PathFollowerStatus.Following || _path.Count == 0)
        {
            return;
        }

        // Invalidate path if any block modification intersects remaining waypoints
        for (int i = CurrentWaypointIndex; i < _path.Count; i++)
        {
            var wp = _path[i];
            if (e.Position == wp || e.Position == wp.Offset(0, -1, 0) || e.Position == wp.Offset(0, 1, 0))
            {
                if (!Walkability.IsWalkable(wp))
                {
                    Status = PathFollowerStatus.Invalidated;
                    Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        _blockSubscription?.Dispose();
        _blockSubscription = null;
    }
}
