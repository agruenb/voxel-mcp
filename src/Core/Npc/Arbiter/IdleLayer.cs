namespace VoxelKing.Core.Npc.Arbiter;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;

/// <summary>
/// Authoritative implementation of the Idle layer per ARCHITECTURE §5.2 and FEATURES F-208.
/// NPCs wander within 8 blocks of home and look at nearby players.
/// </summary>
public sealed class IdleLayer : IIdleLayer
{
    public const float DefaultWanderRadius = 8.0f;
    public const float DefaultMinWaitTime = 2.0f;
    public const float DefaultMaxWaitTime = 4.5f;
    public const float DefaultPlayerNoticeRadius = 6.0f;

    private readonly NpcState _npc;
    private readonly IVoxelWorld? _world;
    private readonly IPathfindingService? _pathfindingService;
    private readonly EntityRegistry? _registry;
    private readonly IEventBus? _eventBus;
    private readonly IWalkability? _walkability;
    private readonly IRandomProvider _rng;

    private float _waitTimer;
    private IAction? _idleAction;

    public IdleState State { get; private set; } = IdleState.Waiting;
    public Vec3i? CurrentWanderGoal { get; private set; }

    public float WanderRadius { get; set; } = DefaultWanderRadius;
    public float MinWaitTime { get; set; } = DefaultMinWaitTime;
    public float MaxWaitTime { get; set; } = DefaultMaxWaitTime;
    public float PlayerNoticeRadius { get; set; } = DefaultPlayerNoticeRadius;

    public IdleLayer(
        NpcState npc,
        IVoxelWorld? world = null,
        IPathfindingService? pathfindingService = null,
        EntityRegistry? registry = null,
        IEventBus? eventBus = null,
        IWalkability? walkability = null,
        IRandomProvider? rng = null)
    {
        _npc = npc ?? throw new ArgumentNullException(nameof(npc));
        _world = world;
        _pathfindingService = pathfindingService;
        _registry = registry;
        _eventBus = eventBus;
        _walkability = walkability ?? (world != null ? new Walkability(world) : null);
        _rng = rng ?? new DefaultRandomProvider();

        _waitTimer = GetRandomWaitTime();
    }

    /// <inheritdoc />
    public void Tick(float dt)
    {
        // 1. Check if an existing idle action has finished or was replaced
        if (_idleAction != null)
        {
            if (_idleAction.Status.IsCompleted)
            {
                if (ReferenceEquals(_npc.CurrentAction, _idleAction))
                {
                    _npc.CurrentAction = null;
                }

                _idleAction = null;
                CurrentWanderGoal = null;
                State = IdleState.Waiting;
                _waitTimer = GetRandomWaitTime();
                return;
            }

            if (!ReferenceEquals(_npc.CurrentAction, _idleAction))
            {
                // Idle action was superseded externally
                _idleAction = null;
                CurrentWanderGoal = null;
                State = IdleState.Waiting;
                _waitTimer = GetRandomWaitTime();
                return;
            }

            // Idle action is still running
            State = IdleState.Wandering;
            return;
        }

        // If an external non-idle action is actively running, yield to it
        if (_npc.CurrentAction != null && _npc.CurrentAction.Status.IsRunning)
        {
            return;
        }

        // 2. Waiting / Looking at nearby players
        _waitTimer -= dt;

        var nearbyPlayer = FindNearbyPlayer();
        if (nearbyPlayer != null)
        {
            var toPlayer = nearbyPlayer.Position - _npc.Position;
            var hToPlayer = new Vec3(toPlayer.X, 0f, toPlayer.Z);
            if (hToPlayer.LengthSquared() > 0.01f)
            {
                _npc.SetFacingDirection(hToPlayer);
            }

            State = IdleState.LookingAtPlayer;
        }
        else
        {
            State = IdleState.Waiting;
        }

        if (_waitTimer <= 0f)
        {
            TryStartWander();
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        if (_idleAction != null && ReferenceEquals(_npc.CurrentAction, _idleAction))
        {
            _idleAction.Cancel();
            _npc.CurrentAction = null;
        }

        _idleAction = null;
        CurrentWanderGoal = null;
        State = IdleState.Waiting;
        _waitTimer = GetRandomWaitTime();
    }

    private void TryStartWander()
    {
        if (_world == null || _pathfindingService == null || _walkability == null)
        {
            _waitTimer = GetRandomWaitTime();
            return;
        }

        Vec3i? chosenTarget = null;

        // Try up to 10 random candidates within WanderRadius of home
        for (int i = 0; i < 10; i++)
        {
            float theta = _rng.NextSingle() * MathF.Tau;
            float r = MathF.Sqrt(_rng.NextSingle()) * WanderRadius;

            float candidateX = _npc.Home.X + (r * MathF.Cos(theta));
            float candidateZ = _npc.Home.Z + (r * MathF.Sin(theta));

            int cx = (int)MathF.Round(candidateX);
            int cz = (int)MathF.Round(candidateZ);
            int homeY = (int)MathF.Round(_npc.Home.Y);

            var surface = _walkability.Surface(cx, cz, homeY + 4, homeY - 6);
            if (!surface.HasValue)
            {
                continue;
            }

            var cell = surface.Value;

            // Verify cell is within WanderRadius from home
            float distFromHome = MathF.Sqrt(
                MathF.Pow(cell.X + 0.5f - _npc.Home.X, 2) +
                MathF.Pow(cell.Z + 0.5f - _npc.Home.Z, 2));

            if (distFromHome > WanderRadius)
            {
                continue;
            }

            // Verify cell is walkable and not in water
            if (!_walkability.IsWalkable(cell) || Walkability.IsWater(_world, cell))
            {
                continue;
            }

            // Verify distance from NPC current position is at least 1.5 blocks
            float distFromNpc = _npc.Position.Distance(new Vec3(cell.X + 0.5f, cell.Y, cell.Z + 0.5f));
            if (distFromNpc < 1.5f)
            {
                continue;
            }

            chosenTarget = cell;
            break;
        }

        if (chosenTarget.HasValue)
        {
            var options = new GoToOptions
            {
                Tolerance = 0.5f,
                Timeout = 15f,
                MoveSpeed = 2.5f,
                AllowPartial = false
            };

            var action = new GoToAction(
                _npc,
                chosenTarget.Value,
                options,
                _pathfindingService,
                _world,
                _eventBus,
                _walkability);

            action.Start();
            _idleAction = action;
            _npc.CurrentAction = action;
            CurrentWanderGoal = chosenTarget.Value;
            State = IdleState.Wandering;
        }
        else
        {
            // If no suitable wander position was found, wait a short cooldown before retrying
            _waitTimer = 1.5f;
            State = IdleState.Waiting;
        }
    }

    private PlayerEntity? FindNearbyPlayer()
    {
        if (_registry == null)
        {
            return null;
        }

        var nearest = _registry.FindNearest(_npc.Position, PlayerNoticeRadius, EntityKind.Player);
        return nearest as PlayerEntity;
    }

    private float GetRandomWaitTime()
    {
        return MinWaitTime + (_rng.NextSingle() * Math.Max(0.1f, MaxWaitTime - MinWaitTime));
    }
}
