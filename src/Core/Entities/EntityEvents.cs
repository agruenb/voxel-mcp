namespace VoxelKing.Core.Entities;

using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;

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

/// <summary>
/// Event published when an entity speaks text aloud into the world.
/// </summary>
public readonly record struct EntitySpoke(EntityId SpeakerId, string SpeakerName, Vec3 Position, string Text, float HearingRadius = 16.0f);

/// <summary>
/// Event published when an NPC's conversation state changes during interactive dialogue (F-405).
/// </summary>
public readonly record struct NpcConversationStateChanged(
    EntityId NpcId,
    NpcConversationState OldState,
    NpcConversationState NewState);

