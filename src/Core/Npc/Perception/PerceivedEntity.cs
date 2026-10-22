namespace VoxelKing.Core.Npc.Perception;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;

/// <summary>
/// Immutable perception snapshot of an entity matching the world.entity(id) Lua API schema.
/// </summary>
public sealed record PerceivedEntity(
    EntityId Id,
    EntityKind Kind,
    string Name,
    Vec3 Position,
    float Health,
    string? ItemId = null,
    int? Count = null)
{
    /// <summary>
    /// String identifier for kind ("player", "npc", "item", "animal").
    /// </summary>
    public string KindString => Kind.ToApiString();

    /// <summary>
    /// Constructs a perception snapshot from a live simulation entity.
    /// </summary>
    public static PerceivedEntity FromEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        float health = 100f;
        string? itemId = null;
        int? count = null;

        if (entity is NpcState npc)
        {
            health = npc.Health;
        }
        else if (entity is PlayerEntity player)
        {
            health = player.Health;
        }
        else if (entity is ItemEntity item)
        {
            health = 1f;
            itemId = item.Stack.ItemId;
            count = item.Stack.Count;
        }

        return new PerceivedEntity(
            entity.Id,
            entity.Kind,
            entity.Name,
            entity.Position,
            health,
            itemId,
            count);
    }
}
