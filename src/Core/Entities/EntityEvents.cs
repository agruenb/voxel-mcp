namespace VoxelKing.Core.Entities;

/// <summary>
/// Event published when an entity is spawned into the world simulation.
/// </summary>
public readonly record struct EntitySpawned(IEntity Entity);

/// <summary>
/// Event published when an entity is despawned from the world simulation.
/// </summary>
public readonly record struct EntityDespawned(EntityId Id, EntityKind Kind);

/// <summary>
/// Event published when an item entity is picked up by an entity with an inventory.
/// </summary>
public readonly record struct ItemPickedUp(EntityId PickerId, EntityId ItemId, string ItemName, int Count);
