namespace VoxelKing.Core.Npc.Perception;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Scoped perception queries evaluated from the perspective of an individual NPC observer,
/// implementing the exact semantics of Lua Brain API §3.4 (world.*).
/// </summary>
public interface INpcPerception
{
    /// <summary>
    /// Returns the block type string at the given position, or null if outside perception radius.
    /// (world.block(pos))
    /// </summary>
    string? GetBlock(Vec3i pos);

    /// <summary>
    /// Finds the nearest block of the specified type within perception radius (clamped to <= 24).
    /// (world.find_block(block_id [, radius]))
    /// </summary>
    Vec3i? FindBlock(string blockId, int? radius = null);

    /// <summary>
    /// Finds all blocks of the specified type within perception radius, ordered nearest-first (max <= 64).
    /// (world.find_blocks(block_id [, radius [, max]]))
    /// </summary>
    IReadOnlyList<Vec3i> FindBlocks(string blockId, int? radius = null, int max = 64);

    /// <summary>
    /// Finds the nearest active entity of the specified kind within perception radius (clamped to <= 32).
    /// (world.find_entity(kind [, radius]))
    /// </summary>
    EntityId? FindEntity(EntityKind kind, float? radius = null);

    /// <summary>
    /// Finds the nearest active entity of the specified kind string ("player", "npc", "item", "animal").
    /// (world.find_entity(kind [, radius]))
    /// </summary>
    EntityId? FindEntity(string kindString, float? radius = null);

    /// <summary>
    /// Finds all active entities of the specified kind within perception radius, ordered nearest-first.
    /// (world.find_entities(kind [, radius]))
    /// </summary>
    IReadOnlyList<EntityId> FindEntities(EntityKind kind, float? radius = null);

    /// <summary>
    /// Finds all active entities of the specified kind string within perception radius, ordered nearest-first.
    /// (world.find_entities(kind [, radius]))
    /// </summary>
    IReadOnlyList<EntityId> FindEntities(string kindString, float? radius = null);

    /// <summary>
    /// Returns a perception snapshot of an entity, or null if not perceivable (missing or outside radius).
    /// (world.entity(id))
    /// </summary>
    PerceivedEntity? GetEntity(EntityId id);

    /// <summary>
    /// Returns whether the cell at pos is walkable and within perception radius.
    /// (world.is_walkable(pos))
    /// </summary>
    bool IsWalkable(Vec3i pos);

    /// <summary>
    /// Returns the highest walkable cell in column (x, z) within perception radius.
    /// (world.surface(x, z))
    /// </summary>
    Vec3i? GetSurface(int x, int z);

    /// <summary>
    /// Returns the current world time of day in hours (0.0 to 24.0).
    /// (world.time())
    /// </summary>
    float GetTime();

    /// <summary>
    /// Returns whether it is currently daytime in the world.
    /// (world.is_day())
    /// </summary>
    bool IsDay();
}
