namespace VoxelKing.Core.Npc.Perception;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;

/// <summary>
/// Service implementing spatial perception rules over voxels and entities,
/// scoping queries by perception radius and returning results ordered nearest-first.
/// </summary>
public sealed class PerceptionService : IPerceptionService
{
    public const int DefaultBlockRadius = 24;
    public const int DefaultEntityRadius = 32;

    public int BlockRadius { get; }
    public int EntityRadius { get; }
    public IVoxelWorld World { get; }
    public EntityRegistry EntityRegistry { get; }
    public IWalkability Walkability { get; }
    public IWorldTime WorldTime { get; }

    public PerceptionService(
        IVoxelWorld world,
        EntityRegistry entityRegistry,
        WorldConfig? config = null,
        IWalkability? walkability = null,
        IWorldTime? worldTime = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        EntityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        BlockRadius = config?.PerceptionRadiusBlocks ?? DefaultBlockRadius;
        EntityRadius = config?.PerceptionRadiusEntities ?? DefaultEntityRadius;
        Walkability = walkability ?? new Walkability(world);
        WorldTime = worldTime ?? (config != null
            ? new WorldTime(config.InitialHour, config.DayLengthSeconds, config.DayStartHour, config.DayEndHour)
            : new WorldTime());
    }

    public PerceptionService(
        IVoxelWorld world,
        EntityRegistry entityRegistry,
        int blockRadius,
        int entityRadius,
        IWalkability? walkability = null,
        IWorldTime? worldTime = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        EntityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        BlockRadius = Math.Max(1, blockRadius);
        EntityRadius = Math.Max(1, entityRadius);
        Walkability = walkability ?? new Walkability(world);
        WorldTime = worldTime ?? new WorldTime();
    }

    /// <inheritdoc />
    public string? GetBlock(Vec3 observerPos, Vec3i targetPos)
    {
        var targetCenter = new Vec3(targetPos.X + 0.5f, targetPos.Y + 0.5f, targetPos.Z + 0.5f);
        if (observerPos.Distance(targetCenter) > BlockRadius)
        {
            return null;
        }

        ushort blockId = World.GetBlock(targetPos);
        return World.BlockRegistry.TryGetDefinition(blockId, out var def) ? def.StringId : null;
    }

    /// <inheritdoc />
    public Vec3i? FindBlock(Vec3 observerPos, string blockId, int? radius = null)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        int queryRadius = radius.HasValue ? Math.Clamp(radius.Value, 0, BlockRadius) : BlockRadius;
        if (queryRadius <= 0)
        {
            return null;
        }

        return World.FindNearest(blockId, observerPos.FloorToInt(), queryRadius);
    }

    /// <inheritdoc />
    public IReadOnlyList<Vec3i> FindBlocks(Vec3 observerPos, string blockId, int? radius = null, int max = 64)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return Array.Empty<Vec3i>();
        }

        int queryRadius = radius.HasValue ? Math.Clamp(radius.Value, 0, BlockRadius) : BlockRadius;
        int queryMax = Math.Clamp(max, 1, 64);

        if (queryRadius <= 0)
        {
            return Array.Empty<Vec3i>();
        }

        return World.FindAll(blockId, observerPos.FloorToInt(), queryRadius, queryMax);
    }

    /// <inheritdoc />
    public EntityId? FindEntity(Vec3 observerPos, EntityKind kind, float? radius = null, EntityId? excludeId = null)
    {
        float queryRadius = radius.HasValue ? Math.Clamp(radius.Value, 0f, EntityRadius) : EntityRadius;
        if (queryRadius <= 0f)
        {
            return null;
        }

        if (excludeId == null)
        {
            var entity = EntityRegistry.FindNearest(observerPos, queryRadius, kind);
            return entity?.Id;
        }

        var entities = EntityRegistry.FindAll(observerPos, queryRadius, kind);
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i].Id != excludeId.Value)
            {
                return entities[i].Id;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public EntityId? FindEntity(Vec3 observerPos, string kindString, float? radius = null, EntityId? excludeId = null)
    {
        return EntityKindExtensions.TryParse(kindString, out var kind)
            ? FindEntity(observerPos, kind, radius, excludeId)
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<EntityId> FindEntities(Vec3 observerPos, EntityKind kind, float? radius = null, EntityId? excludeId = null)
    {
        float queryRadius = radius.HasValue ? Math.Clamp(radius.Value, 0f, EntityRadius) : EntityRadius;
        if (queryRadius <= 0f)
        {
            return Array.Empty<EntityId>();
        }

        var entities = EntityRegistry.FindAll(observerPos, queryRadius, kind);
        var result = new List<EntityId>(entities.Count);

        for (int i = 0; i < entities.Count; i++)
        {
            if (excludeId.HasValue && entities[i].Id == excludeId.Value)
            {
                continue;
            }

            result.Add(entities[i].Id);
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<EntityId> FindEntities(Vec3 observerPos, string kindString, float? radius = null, EntityId? excludeId = null)
    {
        return EntityKindExtensions.TryParse(kindString, out var kind)
            ? FindEntities(observerPos, kind, radius, excludeId)
            : Array.Empty<EntityId>();
    }

    /// <inheritdoc />
    public PerceivedEntity? GetEntity(Vec3 observerPos, EntityId entityId)
    {
        if (!EntityRegistry.TryGet(entityId, out var entity) || !entity.IsActive)
        {
            return null;
        }

        if (observerPos.Distance(entity.Position) > EntityRadius)
        {
            return null;
        }

        return PerceivedEntity.FromEntity(entity);
    }

    /// <inheritdoc />
    public bool IsWalkable(Vec3 observerPos, Vec3i targetPos)
    {
        var targetCenter = new Vec3(targetPos.X + 0.5f, targetPos.Y + 0.5f, targetPos.Z + 0.5f);
        if (observerPos.Distance(targetCenter) > BlockRadius)
        {
            return false;
        }

        return Walkability.IsWalkable(targetPos);
    }

    /// <inheritdoc />
    public Vec3i? GetSurface(Vec3 observerPos, int x, int z)
    {
        float dx = (x + 0.5f) - observerPos.X;
        float dz = (z + 0.5f) - observerPos.Z;
        if ((dx * dx + dz * dz) > BlockRadius * BlockRadius)
        {
            return null;
        }

        int minY = (int)MathF.Floor(observerPos.Y - BlockRadius);
        int maxY = (int)MathF.Ceiling(observerPos.Y + BlockRadius);

        var surface = Walkability.Surface(x, z, maxY, minY);
        if (!surface.HasValue)
        {
            return null;
        }

        var surfaceCenter = new Vec3(surface.Value.X + 0.5f, surface.Value.Y + 0.5f, surface.Value.Z + 0.5f);
        if (observerPos.Distance(surfaceCenter) > BlockRadius)
        {
            return null;
        }

        return surface.Value;
    }

    /// <inheritdoc />
    public float GetTime() => WorldTime.TimeOfDayHours;

    /// <inheritdoc />
    public bool IsDay() => WorldTime.IsDay;

    /// <inheritdoc />
    public INpcPerception For(NpcState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        return new NpcPerceptionContext(this, npc);
    }

    private sealed class NpcPerceptionContext : INpcPerception
    {
        private readonly PerceptionService _service;
        private readonly NpcState _npc;

        public NpcPerceptionContext(PerceptionService service, NpcState npc)
        {
            _service = service;
            _npc = npc;
        }

        public string? GetBlock(Vec3i pos) => _service.GetBlock(_npc.Position, pos);

        public Vec3i? FindBlock(string blockId, int? radius = null) =>
            _service.FindBlock(_npc.Position, blockId, radius);

        public IReadOnlyList<Vec3i> FindBlocks(string blockId, int? radius = null, int max = 64) =>
            _service.FindBlocks(_npc.Position, blockId, radius, max);

        public EntityId? FindEntity(EntityKind kind, float? radius = null) =>
            _service.FindEntity(_npc.Position, kind, radius, _npc.Id);

        public EntityId? FindEntity(string kindString, float? radius = null) =>
            _service.FindEntity(_npc.Position, kindString, radius, _npc.Id);

        public IReadOnlyList<EntityId> FindEntities(EntityKind kind, float? radius = null) =>
            _service.FindEntities(_npc.Position, kind, radius, _npc.Id);

        public IReadOnlyList<EntityId> FindEntities(string kindString, float? radius = null) =>
            _service.FindEntities(_npc.Position, kindString, radius, _npc.Id);

        public PerceivedEntity? GetEntity(EntityId id) => _service.GetEntity(_npc.Position, id);

        public bool IsWalkable(Vec3i pos) => _service.IsWalkable(_npc.Position, pos);

        public Vec3i? GetSurface(int x, int z) => _service.GetSurface(_npc.Position, x, z);

        public float GetTime() => _service.GetTime();

        public bool IsDay() => _service.IsDay();
    }
}
