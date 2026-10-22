namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc.Perception;

/// <summary>
/// Deterministic mock perception service implementing <see cref="INpcPerception"/> for
/// brain validation dry runs (LLM_INTEGRATION §4 Stage 5).
/// Returns plausible deterministic values and handles nil cases for perception queries.
/// </summary>
public class MockWorld : INpcPerception
{
    private readonly Vec3 _observerPos;
    private readonly Dictionary<Vec3i, string> _blocks = new();
    private readonly Dictionary<EntityId, PerceivedEntity> _entities = new();
    private float _time = 12.0f;
    private bool _isDay = true;

    /// <summary>
    /// Gets the observer position in the mock world.
    /// </summary>
    public Vec3 ObserverPos => _observerPos;

    /// <summary>
    /// Explicit block map for specific coordinate lookups.
    /// </summary>
    public IDictionary<Vec3i, string> Blocks => _blocks;

    /// <summary>
    /// Registered entities perceptible in this mock world.
    /// </summary>
    public IDictionary<EntityId, PerceivedEntity> Entities => _entities;

    /// <summary>
    /// Gets or sets the mock world time in hours (0..24).
    /// </summary>
    public float Time
    {
        get => _time;
        set => _time = value;
    }

    /// <summary>
    /// Gets or sets whether it is currently daytime in the mock world.
    /// </summary>
    public bool IsDayTime
    {
        get => _isDay;
        set => _isDay = value;
    }

    public MockWorld(Vec3? observerPos = null)
    {
        _observerPos = observerPos ?? new Vec3(0, 64, 0);
        PopulateDefaults();
    }

    private void PopulateDefaults()
    {
        var origin = _observerPos.FloorToInt();

        // Plausible default blocks near the observer
        _blocks[origin + new Vec3i(3, 0, 0)] = "oak_log";
        _blocks[origin + new Vec3i(3, 1, 0)] = "oak_log";
        _blocks[origin + new Vec3i(4, 0, 0)] = "oak_log";
        _blocks[origin + new Vec3i(5, 0, 0)] = "chest";
        _blocks[origin + new Vec3i(2, 0, 1)] = "wheat";
        _blocks[origin + new Vec3i(0, -1, 0)] = "grass";
        _blocks[origin + new Vec3i(0, -2, 0)] = "dirt";
        _blocks[origin + new Vec3i(0, -3, 0)] = "stone";

        // Plausible default entities
        var playerEntity = new PerceivedEntity(
            new EntityId(100),
            EntityKind.Player,
            "Player",
            _observerPos + new Vec3(2f, 0f, 2f),
            100f);

        var itemEntity = new PerceivedEntity(
            new EntityId(200),
            EntityKind.Item,
            "Item_oak_log",
            _observerPos + new Vec3(1f, 0f, 1f),
            1f,
            ItemId: "oak_log",
            Count: 1);

        var npcEntity = new PerceivedEntity(
            new EntityId(300),
            EntityKind.Npc,
            "CompanionBot",
            _observerPos + new Vec3(4f, 0f, 0f),
            100f);

        var animalEntity = new PerceivedEntity(
            new EntityId(400),
            EntityKind.Animal,
            "Sheep",
            _observerPos + new Vec3(5f, 0f, 1f),
            20f);

        _entities[playerEntity.Id] = playerEntity;
        _entities[itemEntity.Id] = itemEntity;
        _entities[npcEntity.Id] = npcEntity;
        _entities[animalEntity.Id] = animalEntity;
    }

    /// <inheritdoc />
    public string? GetBlock(Vec3i pos)
    {
        var obsI = _observerPos.FloorToInt();
        if (pos.Distance(obsI) > 24f)
        {
            return null; // outside perception radius (nil case)
        }

        if (_blocks.TryGetValue(pos, out var block))
        {
            return block;
        }

        // Plausible default layers based on height
        if (pos.Y < 64)
        {
            return pos.Y >= 60 ? "dirt" : "stone";
        }
        if (pos.Y == 64)
        {
            return "grass";
        }
        return "air";
    }

    /// <inheritdoc />
    public Vec3i? FindBlock(string blockId, int? radius = null)
    {
        var blocks = FindBlocks(blockId, radius, max: 1);
        return blocks.Count > 0 ? blocks[0] : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<Vec3i> FindBlocks(string blockId, int? radius = null, int max = 64)
    {
        if (string.IsNullOrWhiteSpace(blockId)) return Array.Empty<Vec3i>();

        int effectiveRadius = radius.HasValue ? Math.Clamp(radius.Value, 0, 24) : 24;
        if (effectiveRadius <= 0 || max <= 0) return Array.Empty<Vec3i>();

        var obsI = _observerPos.FloorToInt();
        var matches = new List<Vec3i>();

        // 1. Check explicit block map
        foreach (var (pos, id) in _blocks)
        {
            if (string.Equals(id, blockId, StringComparison.OrdinalIgnoreCase))
            {
                if (pos.Distance(obsI) <= effectiveRadius)
                {
                    matches.Add(pos);
                    if (matches.Count >= max) return matches;
                }
            }
        }

        // 2. Fallback for common types if not explicitly placed in block map
        if (matches.Count == 0)
        {
            if (string.Equals(blockId, "stone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(blockId, "cobblestone", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(obsI + new Vec3i(0, -3, 0));
            }
            else if (string.Equals(blockId, "dirt", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(obsI + new Vec3i(0, -2, 0));
            }
            else if (string.Equals(blockId, "grass", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(obsI + new Vec3i(0, -1, 0));
            }
        }

        return matches.OrderBy(p => p.Distance(obsI)).Take(max).ToList();
    }

    /// <inheritdoc />
    public EntityId? FindEntity(EntityKind kind, float? radius = null)
    {
        var entities = FindEntities(kind, radius);
        return entities.Count > 0 ? entities[0] : null;
    }

    /// <inheritdoc />
    public EntityId? FindEntity(string kindString, float? radius = null)
    {
        if (EntityKindExtensions.TryParse(kindString, out var kind))
        {
            return FindEntity(kind, radius);
        }
        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<EntityId> FindEntities(EntityKind kind, float? radius = null)
    {
        float effectiveRadius = radius.HasValue ? Math.Clamp(radius.Value, 0f, 32f) : 32f;
        if (effectiveRadius <= 0f) return Array.Empty<EntityId>();

        return _entities.Values
            .Where(e => e.Kind == kind && e.Position.Distance(_observerPos) <= effectiveRadius)
            .OrderBy(e => e.Position.Distance(_observerPos))
            .Select(e => e.Id)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<EntityId> FindEntities(string kindString, float? radius = null)
    {
        if (EntityKindExtensions.TryParse(kindString, out var kind))
        {
            return FindEntities(kind, radius);
        }
        return Array.Empty<EntityId>();
    }

    /// <inheritdoc />
    public PerceivedEntity? GetEntity(EntityId id)
    {
        if (_entities.TryGetValue(id, out var entity))
        {
            if (entity.Position.Distance(_observerPos) <= 32f)
            {
                return entity;
            }
        }
        return null; // not found or outside perception radius (nil case)
    }

    /// <inheritdoc />
    public bool IsWalkable(Vec3i pos)
    {
        var obsI = _observerPos.FloorToInt();
        if (pos.Distance(obsI) > 24f) return false;

        // Plausible surface level
        return pos.Y >= 63 && pos.Y <= 65;
    }

    /// <inheritdoc />
    public Vec3i? GetSurface(int x, int z)
    {
        var obsI = _observerPos.FloorToInt();
        var target = new Vec3i(x, 64, z);
        if (target.Distance(obsI) > 24f) return null;

        return target;
    }

    /// <inheritdoc />
    public float GetTime() => _time;

    /// <inheritdoc />
    public bool IsDay() => _isDay;
}
