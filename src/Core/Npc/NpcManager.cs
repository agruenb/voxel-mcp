namespace VoxelKing.Core.Npc;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc.Arbiter;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Perception;
using VoxelKing.Core.Voxel;

/// <summary>
/// Manages NPC lifecycle, physics updates, distance-based LOD tiers, and staggered brain tick scheduling.
/// </summary>
public sealed class NpcManager
{
    public const float DefaultFreezeDistance = 192.0f;
    public const float DefaultItemPickupRadius = 1.5f;
    public const float DefaultGravity = 20.0f;

    private readonly EntityRegistry _registry;
    private readonly NpcConfig _config;
    private readonly IVoxelWorld? _voxelWorld;
    private readonly IEventBus? _eventBus;
    private readonly ILogger? _logger;

    private readonly Dictionary<EntityId, NpcState> _npcsById = new();
    private readonly Dictionary<EntityId, ItemEntity> _itemsById = new();
    private readonly Dictionary<EntityId, (NpcLodTier Tier, int Phase)> _npcScheduling = new();

    private readonly List<NpcState>[] _normalBuckets;
    private readonly List<NpcState>[] _distantBuckets;
    private readonly HashSet<NpcState> _frozenNpcs = new();

    private readonly int _normalInterval;
    private readonly int _distantInterval;
    private float _brainTimeAccumulator;
    private int _brainTickIndex;

    /// <summary>
    /// Raised whenever an NPC's brain ticks in the staggered scheduler.
    /// </summary>
    public event Action<NpcState>? NpcBrainTicked;

    public EntityRegistry Registry => _registry;
    public NpcConfig Config => _config;
    public IVoxelWorld? VoxelWorld => _voxelWorld;
    public IEventBus? EventBus => _eventBus;
    public ILogger? Logger => _logger;

    public float FreezeDistance { get; set; } = DefaultFreezeDistance;
    public float ItemPickupRadius { get; set; } = DefaultItemPickupRadius;
    public float Gravity { get; set; } = DefaultGravity;
    public IPathfindingService? PathfindingService { get; set; }
    public ContainerManager? ContainerManager { get; set; }
    public IBlockRegistry? BlockRegistry { get; set; }
    public IPerceptionService? PerceptionService { get; set; }
    public BrainLimitsConfig? BrainLimitsConfig { get; set; }
    public bool EnableArbiters { get; set; } = true;

    public int TotalBrainTicks { get; private set; }
    public int TotalPhysicsTicks { get; private set; }
    public int NormalInterval => _normalInterval;
    public int DistantInterval => _distantInterval;

    public IReadOnlyCollection<NpcState> ActiveNpcs => _npcsById.Values;
    public IReadOnlyCollection<ItemEntity> ActiveItems => _itemsById.Values;

    public NpcManager(
        EntityRegistry registry,
        NpcConfig? config = null,
        IVoxelWorld? voxelWorld = null,
        IEventBus? eventBus = null,
        ILogger? logger = null,
        IPathfindingService? pathfindingService = null,
        IPerceptionService? perceptionService = null,
        BrainLimitsConfig? brainLimitsConfig = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _config = config ?? new NpcConfig();
        _voxelWorld = voxelWorld;
        _eventBus = eventBus;
        _logger = logger;
        PathfindingService = pathfindingService;
        PerceptionService = perceptionService ?? (voxelWorld != null ? new PerceptionService(voxelWorld, _registry) : null);
        BrainLimitsConfig = brainLimitsConfig;

        int physicsHz = Math.Max(1, _config.PhysicsTickRateHz);
        int brainHz = Math.Max(1, _config.BrainTickRateHz);
        int distantHz = Math.Max(1, _config.DistantBrainTickRateHz);

        _normalInterval = Math.Max(1, physicsHz / brainHz);
        _distantInterval = Math.Max(1, physicsHz / distantHz);

        _normalBuckets = new List<NpcState>[_normalInterval];
        for (int i = 0; i < _normalInterval; i++)
        {
            _normalBuckets[i] = new List<NpcState>();
        }

        _distantBuckets = new List<NpcState>[_distantInterval];
        for (int i = 0; i < _distantInterval; i++)
        {
            _distantBuckets[i] = new List<NpcState>();
        }
    }

    /// <summary>
    /// Spawns a new NPC in the world and registers it with the entity registry and staggered scheduler.
    /// </summary>
    public NpcState SpawnNpc(
        string name,
        Vec3 position,
        string profile = "",
        Vec3? home = null,
        IInventory? inventory = null,
        float health = NpcState.DefaultMaxHealth,
        float facing = 0f,
        INpcMemory? memory = null,
        IBehaviourArbiter? arbiter = null)
    {
        var id = _registry.GenerateNextId();
        var npcMemory = memory ?? new NpcMemory(BrainLimitsConfig);
        var npc = new NpcState(id, name, position, profile, home, inventory, health, facing, npcMemory);

        _registry.Register(npc);
        _npcsById[id] = npc;

        // Assign initial LOD tier based on distance to player
        var tier = CalculateLodTier(npc.Position);
        AssignNpcToTier(npc, tier);

        if (PerceptionService != null)
        {
            npc.Perception = PerceptionService.For(npc);
        }

        if (arbiter != null)
        {
            npc.Arbiter = arbiter;
        }
        else if (EnableArbiters && _voxelWorld != null)
        {
            var reflex = new ReflexLayer(npc, _voxelWorld);
            var task = new StubTaskLayer();
            var idle = new IdleLayer(npc, _voxelWorld, PathfindingService, _registry, _eventBus);
            npc.Arbiter = new BehaviourArbiter(npc, reflex, task, idle);
        }

        _eventBus?.Publish(new EntitySpawned(npc));
        _logger?.LogDebug($"Spawned NPC '{name}' (ID {id}) at {position} in LOD {tier}");

        return npc;
    }

    /// <summary>
    /// Despawns an NPC from the world and removes it from scheduling.
    /// </summary>
    public bool DespawnNpc(EntityId id)
    {
        if (!_npcsById.Remove(id, out var npc))
        {
            return false;
        }

        RemoveNpcFromScheduling(npc);
        _registry.Unregister(id);

        _eventBus?.Publish(new EntityDespawned(id, EntityKind.Npc));
        _logger?.LogDebug($"Despawned NPC '{npc.Name}' (ID {id})");

        return true;
    }

    /// <summary>
    /// Spawns a dropped item entity in the world.
    /// </summary>
    public ItemEntity SpawnItem(
        ItemStack stack,
        Vec3 position,
        Vec3? velocity = null,
        float pickupCooldown = 0.5f)
    {
        var id = _registry.GenerateNextId();
        var item = new ItemEntity(id, stack, position, velocity, pickupCooldown);

        _registry.Register(item);
        _itemsById[id] = item;

        _eventBus?.Publish(new EntitySpawned(item));
        return item;
    }

    /// <summary>
    /// Spawns a dropped item entity by item ID and count.
    /// </summary>
    public ItemEntity SpawnItem(
        string itemId,
        int count,
        Vec3 position,
        Vec3? velocity = null,
        float pickupCooldown = 0.5f)
    {
        return SpawnItem(new ItemStack(itemId, count), position, velocity, pickupCooldown);
    }

    /// <summary>
    /// Despawns an item entity from the world.
    /// </summary>
    public bool DespawnItem(EntityId id)
    {
        if (!_itemsById.Remove(id, out var item))
        {
            return false;
        }

        item.MarkCollected();
        _registry.Unregister(id);

        _eventBus?.Publish(new EntityDespawned(id, EntityKind.Item));
        return true;
    }

    /// <summary>
    /// Executes a continuous physics tick, integrating velocities, applying item gravity,
    /// and checking auto-pickup for dropped items near entities with inventories.
    /// </summary>
    public void PhysicsTick(float dt)
    {
        TotalPhysicsTicks++;

        // 1. Update dropped item entities
        if (_itemsById.Count > 0)
        {
            var items = _itemsById.Values.ToArray();
            foreach (var item in items)
            {
                if (item.IsCollected)
                {
                    DespawnItem(item.Id);
                    continue;
                }

                item.TickPhysics(dt, _voxelWorld, Gravity);
                _registry.UpdateSpatial(item);

                // Auto-pickup check for items whose cooldown has expired
                if (item.CanPickup)
                {
                    CheckItemPickup(item);
                }
            }
        }

        // 2. Tick queued pathfinding requests within frame budget
        PathfindingService?.Tick();

        // 3. Update active NPC actions, path followers, positions, and kinematic collisions
        if (_npcsById.Count > 0)
        {
            foreach (var npc in _npcsById.Values)
            {
                npc.Arbiter?.PhysicsTick(dt);

                if (npc.CurrentAction != null && npc.CurrentAction.Status.IsRunning)
                {
                    npc.CurrentAction.Tick(dt);
                }
                else
                {
                    npc.PathFollower?.Update(dt);
                }

                if (_voxelWorld != null)
                {
                    KinematicMover.Move(npc, _voxelWorld, dt, Gravity, npc.StepHeight);
                }
                else if (npc.Velocity.LengthSquared() > 1e-6f)
                {
                    npc.Position += npc.Velocity * dt;
                }

                _registry.UpdateSpatial(npc);
            }
        }
    }

    /// <summary>
    /// Advances brain simulation time and executes staggered brain ticks for NPCs whose turn it is.
    /// </summary>
    public void BrainTick(float dt)
    {
        _brainTimeAccumulator += dt;
        float tickPeriod = 1.0f / Math.Max(1, _config.PhysicsTickRateHz);

        while (_brainTimeAccumulator >= tickPeriod)
        {
            _brainTimeAccumulator -= tickPeriod;
            StepBrainTick();
        }
    }

    /// <summary>
    /// Executes exactly one discrete brain tick step across all scheduled NPC phases.
    /// </summary>
    public void StepBrainTick()
    {
        TotalBrainTicks++;

        // Update LOD tiers for all active NPCs
        UpdateLodTiers();

        int normalPhase = _brainTickIndex % _normalInterval;
        int distantPhase = _brainTickIndex % _distantInterval;
        _brainTickIndex++;

        // Gather NPCs scheduled to tick this step
        var tickingNpcs = new List<NpcState>();

        var normalList = _normalBuckets[normalPhase];
        for (int i = 0; i < normalList.Count; i++)
        {
            tickingNpcs.Add(normalList[i]);
        }

        var distantList = _distantBuckets[distantPhase];
        for (int i = 0; i < distantList.Count; i++)
        {
            tickingNpcs.Add(distantList[i]);
        }

        float tickDt = 1.0f / Math.Max(1, _config.PhysicsTickRateHz);

        // Execute brain tick on scheduled NPCs
        foreach (var npc in tickingNpcs)
        {
            if (!npc.IsActive)
            {
                continue;
            }

            float elapsedSinceLastBrainTick = npc.TimeSinceLastBrainTick > 0f
                ? npc.TimeSinceLastBrainTick
                : (1.0f / Math.Max(1, _config.BrainTickRateHz));

            npc.BrainTickCount++;
            npc.TimeSinceLastBrainTick = 0f;
            npc.Arbiter?.Tick(elapsedSinceLastBrainTick);
            NpcBrainTicked?.Invoke(npc);
        }

        // Update elapsed time since last tick for non-ticking NPCs
        foreach (var npc in _npcsById.Values)
        {
            if (!tickingNpcs.Contains(npc))
            {
                npc.TimeSinceLastBrainTick += tickDt;
            }
        }
    }

    /// <summary>
    /// Combined convenience tick executing physics tick followed by brain tick.
    /// </summary>
    public void Tick(float dt)
    {
        PhysicsTick(dt);
        BrainTick(dt);
    }

    /// <summary>
    /// Retrieves the current LOD tier and assigned phase slot for a registered NPC.
    /// </summary>
    public (NpcLodTier Tier, int Phase) GetNpcSchedule(EntityId id)
    {
        return _npcScheduling.TryGetValue(id, out var schedule)
            ? schedule
            : (NpcLodTier.Frozen, -1);
    }

    /// <summary>
    /// Returns the number of NPCs currently assigned to a given phase bucket of a tier.
    /// </summary>
    public int GetScheduledNpcCount(NpcLodTier tier, int phase)
    {
        return tier switch
        {
            NpcLodTier.Normal => phase >= 0 && phase < _normalInterval ? _normalBuckets[phase].Count : 0,
            NpcLodTier.Distant => phase >= 0 && phase < _distantInterval ? _distantBuckets[phase].Count : 0,
            NpcLodTier.Frozen => _frozenNpcs.Count,
            _ => 0
        };
    }

    private void CheckItemPickup(ItemEntity item)
    {
        var nearby = _registry.FindAll(item.Position, ItemPickupRadius);
        foreach (var entity in nearby)
        {
            IInventory? inventory = null;
            if (entity is PlayerEntity player)
            {
                inventory = player.Inventory;
            }
            else if (entity is NpcState npc)
            {
                inventory = npc.Inventory;
            }

            if (inventory == null)
            {
                continue;
            }

            if (item.TryPickup(inventory, out int pickedUp))
            {
                _eventBus?.Publish(new ItemPickedUp(entity.Id, item.Id, item.Name, pickedUp));

                if (item.IsCollected)
                {
                    DespawnItem(item.Id);
                    break;
                }
            }
        }
    }

    private void UpdateLodTiers()
    {
        if (_npcsById.Count == 0)
        {
            return;
        }

        foreach (var npc in _npcsById.Values)
        {
            var targetTier = CalculateLodTier(npc.Position);
            if (targetTier != npc.LodTier)
            {
                RemoveNpcFromScheduling(npc);
                AssignNpcToTier(npc, targetTier);
            }
        }
    }

    private NpcLodTier CalculateLodTier(Vec3 position)
    {
        float minPlayerDist = float.MaxValue;
        bool hasPlayer = false;

        foreach (var player in _registry.GetAll(EntityKind.Player))
        {
            hasPlayer = true;
            float dist = position.Distance(player.Position);
            if (dist < minPlayerDist)
            {
                minPlayerDist = dist;
            }
        }

        if (!hasPlayer)
        {
            // If no player is registered in the scene, default to Normal tier
            return NpcLodTier.Normal;
        }

        if (minPlayerDist <= _config.LodDistanceThreshold)
        {
            return NpcLodTier.Normal;
        }

        if (minPlayerDist <= FreezeDistance)
        {
            return NpcLodTier.Distant;
        }

        return NpcLodTier.Frozen;
    }

    private void AssignNpcToTier(NpcState npc, NpcLodTier tier)
    {
        npc.LodTier = tier;

        switch (tier)
        {
            case NpcLodTier.Normal:
            {
                int bestPhase = FindLeastPopulatedBucket(_normalBuckets);
                _normalBuckets[bestPhase].Add(npc);
                _npcScheduling[npc.Id] = (NpcLodTier.Normal, bestPhase);
                break;
            }
            case NpcLodTier.Distant:
            {
                int bestPhase = FindLeastPopulatedBucket(_distantBuckets);
                _distantBuckets[bestPhase].Add(npc);
                _npcScheduling[npc.Id] = (NpcLodTier.Distant, bestPhase);
                break;
            }
            case NpcLodTier.Frozen:
            {
                _frozenNpcs.Add(npc);
                _npcScheduling[npc.Id] = (NpcLodTier.Frozen, -1);
                break;
            }
        }
    }

    private void RemoveNpcFromScheduling(NpcState npc)
    {
        if (_npcScheduling.Remove(npc.Id, out var schedule))
        {
            switch (schedule.Tier)
            {
                case NpcLodTier.Normal:
                    if (schedule.Phase >= 0 && schedule.Phase < _normalInterval)
                    {
                        _normalBuckets[schedule.Phase].Remove(npc);
                    }
                    break;
                case NpcLodTier.Distant:
                    if (schedule.Phase >= 0 && schedule.Phase < _distantInterval)
                    {
                        _distantBuckets[schedule.Phase].Remove(npc);
                    }
                    break;
                case NpcLodTier.Frozen:
                    _frozenNpcs.Remove(npc);
                    break;
            }
        }
    }

    private static int FindLeastPopulatedBucket(List<NpcState>[] buckets)
    {
        int minPhase = 0;
        int minCount = buckets[0].Count;

        for (int i = 1; i < buckets.Length; i++)
        {
            if (buckets[i].Count < minCount)
            {
                minCount = buckets[i].Count;
                minPhase = i;
            }
        }

        return minPhase;
    }
}
