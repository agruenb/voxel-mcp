namespace VoxelKing.Core.Npc.Arbiter;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;

/// <summary>
/// Authoritative implementation of the Reflex layer handling immediate hazard avoidance,
/// suffocation relief, and unstuck maneuvers per ARCHITECTURE §5.2 and FEATURES F-208.
/// </summary>
public sealed class ReflexLayer : IReflexLayer
{
    public const float DefaultStuckTimeout = 2.0f;
    public const float DefaultProgressThreshold = 0.15f;

    private readonly NpcState _npc;
    private readonly IVoxelWorld? _world;
    private readonly IWalkability? _walkability;

    private float _stuckTimer;
    private Vec3 _lastProgressPos;

    public bool IsActive { get; private set; }
    public string? CurrentReflexReason { get; private set; }

    public float StuckTimeout { get; set; } = DefaultStuckTimeout;
    public float ProgressThreshold { get; set; } = DefaultProgressThreshold;

    public ReflexLayer(
        NpcState npc,
        IVoxelWorld? world = null,
        IWalkability? walkability = null)
    {
        _npc = npc ?? throw new ArgumentNullException(nameof(npc));
        _world = world;
        _walkability = walkability ?? (world != null ? new Walkability(world) : null);
        _lastProgressPos = npc.Position;
    }

    /// <inheritdoc />
    public bool CheckReflex(float dt)
    {
        if (_world == null || _walkability == null)
        {
            IsActive = false;
            CurrentReflexReason = null;
            return false;
        }

        var feet = _npc.Position.FloorToInt();
        var head = (_npc.Position + new Vec3(0f, 1f, 0f)).FloorToInt();

        // 1. Suffocation check: feet or head inside a solid block
        if (Walkability.IsSolid(_world, feet) || Walkability.IsSolid(_world, head))
        {
            CurrentReflexReason = "suffocation";
            IsActive = true;
            ResolveSuffocation(feet);
            return true;
        }

        // 2. Water check: currently in water
        bool inWater = Walkability.IsWater(_world, feet) || Walkability.IsWater(_world, head);
        if (inWater)
        {
            CurrentReflexReason = "water";
            IsActive = true;
            ResolveInWater(feet);
            return true;
        }

        // 3. Movement hazards: water or drop > 3 ahead in direction of movement
        var hVel = new Vec3(_npc.Velocity.X, 0f, _npc.Velocity.Z);
        var facing = _npc.GetFacingDirection();
        var moveDir = hVel.LengthSquared() > 0.05f
            ? hVel.Normalized()
            : (_npc.PathFollower?.Status == PathFollowerStatus.Following ? facing : Vec3.Zero);

        if (moveDir.LengthSquared() > 0.05f)
        {
            var nextBlock = (_npc.Position + moveDir * 0.8f).FloorToInt();

            // Water hazard ahead
            if (Walkability.IsWater(_world, nextBlock) || Walkability.IsWater(_world, nextBlock.Offset(0, -1, 0)))
            {
                CurrentReflexReason = "water_hazard";
                IsActive = true;
                _npc.Velocity = new Vec3(0f, _npc.Velocity.Y, 0f);
                StepBack(feet);
                return true;
            }

            // Drop > 3 hazard ahead
            int feetY = feet.Y;
            bool hasGroundWithin3 = false;
            for (int y = feetY - 1; y >= feetY - 4; y--)
            {
                if (Walkability.IsSolid(_world, new Vec3i(nextBlock.X, y, nextBlock.Z)))
                {
                    hasGroundWithin3 = true;
                    break;
                }
            }

            if (!hasGroundWithin3)
            {
                CurrentReflexReason = "drop_hazard";
                IsActive = true;
                _npc.Velocity = new Vec3(0f, _npc.Velocity.Y, 0f);
                StepBack(feet);
                return true;
            }
        }

        // 4. Stuck detection: no progress despite attempting to move or pathfollower reporting stuck
        bool isPathFollowerStuck = _npc.PathFollower != null && _npc.PathFollower.Status == PathFollowerStatus.Stuck;

        float distMoved = _npc.Position.Distance(_lastProgressPos);
        if (distMoved >= ProgressThreshold)
        {
            _lastProgressPos = _npc.Position;
            _stuckTimer = 0f;
        }
        else if ((_npc.CurrentAction != null && _npc.CurrentAction.Status.IsRunning) || _npc.IsOnWall || _npc.Velocity.LengthSquared() > 0.05f)
        {
            _stuckTimer += dt;
        }
        else
        {
            _stuckTimer = 0f;
        }

        if (isPathFollowerStuck || _stuckTimer >= StuckTimeout)
        {
            CurrentReflexReason = "stuck";
            IsActive = true;
            _stuckTimer = 0f;
            _lastProgressPos = _npc.Position;

            if (_npc.IsOnGround)
            {
                _npc.Jump();
            }

            _npc.Velocity = new Vec3(0f, _npc.Velocity.Y, 0f);
            return true;
        }

        IsActive = false;
        CurrentReflexReason = null;
        return false;
    }

    /// <inheritdoc />
    public void Reset()
    {
        IsActive = false;
        CurrentReflexReason = null;
        _stuckTimer = 0f;
        _lastProgressPos = _npc.Position;
    }

    private void ResolveSuffocation(Vec3i feet)
    {
        if (_world == null || _walkability == null) return;

        Vec3i? safeCell = null;
        float bestDistSq = float.MaxValue;

        // Search adjacent cells for a walkable or safe non-solid space
        for (int dy = 0; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    if (dx == 0 && dz == 0 && dy == 0) continue;

                    var candidate = new Vec3i(feet.X + dx, feet.Y + dy, feet.Z + dz);
                    if (_walkability.IsWalkable(candidate))
                    {
                        float distSq = (dx * dx) + (dy * dy) + (dz * dz);
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            safeCell = candidate;
                        }
                    }
                }
            }

            if (safeCell.HasValue) break;
        }

        // Fallback: non-solid cell with air for feet and head
        if (!safeCell.HasValue)
        {
            for (int dy = 0; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        var candidate = new Vec3i(feet.X + dx, feet.Y + dy, feet.Z + dz);
                        if (!Walkability.IsSolid(_world, candidate) &&
                            !Walkability.IsSolid(_world, candidate.Offset(0, 1, 0)) &&
                            !Walkability.IsWater(_world, candidate))
                        {
                            float distSq = (dx * dx) + (dy * dy) + (dz * dz);
                            if (distSq < bestDistSq)
                            {
                                bestDistSq = distSq;
                                safeCell = candidate;
                            }
                        }
                    }
                }

                if (safeCell.HasValue) break;
            }
        }

        if (safeCell.HasValue)
        {
            _npc.Position = new Vec3(safeCell.Value.X + 0.5f, safeCell.Value.Y, safeCell.Value.Z + 0.5f);
            _npc.Velocity = Vec3.Zero;
            if (Walkability.IsSolid(_world, safeCell.Value.Offset(0, -1, 0)))
            {
                _npc.IsOnGround = true;
            }
        }
    }

    private void ResolveInWater(Vec3i feet)
    {
        if (_world == null || _walkability == null) return;

        // Swim up
        _npc.Velocity = _npc.Velocity with { Y = MathF.Max(_npc.Velocity.Y, 5.0f) };

        // Search for nearest dry walkable cell within 3 blocks
        Vec3i? dryCell = null;
        float bestDistSq = float.MaxValue;

        for (int r = 1; r <= 3; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dy = -1; dy <= 2; dy++)
                    {
                        var candidate = new Vec3i(feet.X + dx, feet.Y + dy, feet.Z + dz);
                        if (_walkability.IsWalkable(candidate) && !Walkability.IsWater(_world, candidate))
                        {
                            float distSq = (dx * dx) + (dz * dz);
                            if (distSq < bestDistSq)
                            {
                                bestDistSq = distSq;
                                dryCell = candidate;
                            }
                        }
                    }
                }
            }

            if (dryCell.HasValue) break;
        }

        if (dryCell.HasValue)
        {
            var steer = new Vec3(dryCell.Value.X + 0.5f, dryCell.Value.Y, dryCell.Value.Z + 0.5f) - _npc.Position;
            var hSteer = new Vec3(steer.X, 0f, steer.Z);
            if (hSteer.LengthSquared() > 0.01f)
            {
                var dir = hSteer.Normalized();
                _npc.Velocity = new Vec3(dir.X * 3.0f, _npc.Velocity.Y, dir.Z * 3.0f);
                _npc.SetFacingDirection(dir);
            }
        }
    }

    private void StepBack(Vec3i feet)
    {
        var safeCenter = new Vec3(feet.X + 0.5f, _npc.Position.Y, feet.Z + 0.5f);
        var toSafe = safeCenter - _npc.Position;
        var toSafeH = new Vec3(toSafe.X, 0f, toSafe.Z);
        if (toSafeH.LengthSquared() > 1e-4f)
        {
            _npc.Position += toSafeH.Normalized() * 0.15f;
        }
    }
}
