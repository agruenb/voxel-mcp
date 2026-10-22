namespace VoxelKing.Core.Npc.Perception;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;

/// <summary>
/// Service providing scoped spatial perception queries over voxels and entities,
/// enforcing perception radius limits and nearest-first query ordering.
/// </summary>
public interface IPerceptionService
{
    /// <summary>
    /// Maximum perception radius for block queries in meters (default 24).
    /// </summary>
    int BlockRadius { get; }

    /// <summary>
    /// Maximum perception radius for entity queries in meters (default 32).
    /// </summary>
    int EntityRadius { get; }

    /// <summary>
    /// The voxel world being queried.
    /// </summary>
    IVoxelWorld World { get; }

    /// <summary>
    /// The entity registry containing active simulation entities.
    /// </summary>
    EntityRegistry EntityRegistry { get; }

    /// <summary>
    /// Walkability query provider.
    /// </summary>
    IWalkability Walkability { get; }

    /// <summary>
    /// World clock time provider.
    /// </summary>
    IWorldTime WorldTime { get; }

    /// <summary>
    /// Retrieves the block type at targetPos if within BlockRadius of observerPos, or null.
    /// </summary>
    string? GetBlock(Vec3 observerPos, Vec3i targetPos);

    /// <summary>
    /// Finds the nearest block of the given type within radius (clamped to <= BlockRadius).
    /// </summary>
    Vec3i? FindBlock(Vec3 observerPos, string blockId, int? radius = null);

    /// <summary>
    /// Finds all blocks of the given type within radius, ordered nearest-first (max <= 64).
    /// </summary>
    IReadOnlyList<Vec3i> FindBlocks(Vec3 observerPos, string blockId, int? radius = null, int max = 64);

    /// <summary>
    /// Finds the nearest active entity of the specified kind within radius (clamped to <= EntityRadius).
    /// </summary>
    EntityId? FindEntity(Vec3 observerPos, EntityKind kind, float? radius = null, EntityId? excludeId = null);

    /// <summary>
    /// Finds the nearest active entity of the specified kind string within radius.
    /// </summary>
    EntityId? FindEntity(Vec3 observerPos, string kindString, float? radius = null, EntityId? excludeId = null);

    /// <summary>
    /// Finds all active entities of the specified kind within radius, ordered nearest-first.
    /// </summary>
    IReadOnlyList<EntityId> FindEntities(Vec3 observerPos, EntityKind kind, float? radius = null, EntityId? excludeId = null);

    /// <summary>
    /// Finds all active entities of the specified kind string within radius, ordered nearest-first.
    /// </summary>
    IReadOnlyList<EntityId> FindEntities(Vec3 observerPos, string kindString, float? radius = null, EntityId? excludeId = null);

    /// <summary>
    /// Retrieves a perception snapshot of an entity if within EntityRadius of observerPos, or null.
    /// </summary>
    PerceivedEntity? GetEntity(Vec3 observerPos, EntityId entityId);

    /// <summary>
    /// Checks whether the cell at targetPos is walkable and within BlockRadius of observerPos.
    /// </summary>
    bool IsWalkable(Vec3 observerPos, Vec3i targetPos);

    /// <summary>
    /// Finds the highest walkable cell in column (x, z) within BlockRadius of observerPos.
    /// </summary>
    Vec3i? GetSurface(Vec3 observerPos, int x, int z);

    /// <summary>
    /// Current world time of day in hours.
    /// </summary>
    float GetTime();

    /// <summary>
    /// Whether it is currently daytime.
    /// </summary>
    bool IsDay();

    /// <summary>
    /// Creates a scoped perception view evaluated from the perspective of the specified NPC.
    /// </summary>
    INpcPerception For(NpcState npc);
}
