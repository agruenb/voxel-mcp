namespace VoxelKing.Core.Persistence;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc.Memory;

public static class PersistenceJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Data transfer object for an individual inventory slot.
/// </summary>
public sealed class InventorySlotDto
{
    public int Slot { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Count { get; set; }
    public int MaxStack { get; set; } = ItemStack.DefaultMaxStack;

    public static List<InventorySlotDto> FromInventory(IInventory? inventory)
    {
        var list = new List<InventorySlotDto>();
        if (inventory == null) return list;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var stack = inventory.GetSlot(i);
            if (stack != null && !stack.IsEmpty)
            {
                list.Add(new InventorySlotDto
                {
                    Slot = i,
                    ItemId = stack.ItemId,
                    Count = stack.Count,
                    MaxStack = stack.MaxStack
                });
            }
        }

        return list;
    }

    public static void PopulateInventory(IInventory inventory, IEnumerable<InventorySlotDto>? slots)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        inventory.Clear();

        if (slots == null) return;

        foreach (var slotDto in slots)
        {
            if (slotDto.Slot >= 0 && slotDto.Slot < inventory.SlotCount && !string.IsNullOrWhiteSpace(slotDto.ItemId) && slotDto.Count > 0)
            {
                inventory.SetSlot(slotDto.Slot, new ItemStack(slotDto.ItemId, slotDto.Count, slotDto.MaxStack));
            }
        }
    }
}

/// <summary>
/// Data transfer object for a world container (chest) inventory.
/// </summary>
public sealed class ContainerSaveDto
{
    public Vec3i Position { get; set; }
    public int SlotCount { get; set; } = 27;
    public List<InventorySlotDto> Slots { get; set; } = new();
}

/// <summary>
/// Data transfer object for world metadata and persistent structures (world.json).
/// </summary>
public sealed class WorldSaveData
{
    public int Version { get; set; } = 1;
    public string WorldName { get; set; } = "default";
    public int Seed { get; set; } = 1337;
    public double WorldTime { get; set; }
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, Vec3i> NamedPlaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ContainerSaveDto> Containers { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, PersistenceJsonOptions.Default);

    public static WorldSaveData FromJson(string json) =>
        JsonSerializer.Deserialize<WorldSaveData>(json, PersistenceJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to deserialize WorldSaveData from JSON.");
}

/// <summary>
/// Data transfer object for a spoken conversation turn.
/// </summary>
public sealed class ConversationTurnDto
{
    public string Speaker { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public ConversationTurnDto() { }
    public ConversationTurnDto(string speaker, string message)
    {
        Speaker = speaker;
        Message = message;
    }
}

/// <summary>
/// Data transfer object for an individual NPC state.
/// </summary>
public sealed class NpcSaveDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public string AppearanceColor { get; set; } = string.Empty;
    public Vec3 Position { get; set; }
    public Vec3 Velocity { get; set; }
    public float Facing { get; set; }
    public float Health { get; set; } = 100f;
    public float MaxHealth { get; set; } = 100f;
    public Vec3 Home { get; set; }
    public bool HasExplicitHome { get; set; }
    public string? CurrentTaskSummary { get; set; }
    public double TaskStartTime { get; set; }
    public int? InstructorId { get; set; }
    public List<InventorySlotDto> Inventory { get; set; } = new();
    public NpcMemoryDto Memory { get; set; } = new();
    public string? BrainSource { get; set; }
    public List<ConversationTurnDto> ConversationHistory { get; set; } = new();
}

/// <summary>
/// Data transfer object for all NPCs in the world (npcs.json).
/// </summary>
public sealed class NpcSaveData
{
    public int Version { get; set; } = 1;
    public List<NpcSaveDto> Npcs { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, PersistenceJsonOptions.Default);

    public static NpcSaveData FromJson(string json) =>
        JsonSerializer.Deserialize<NpcSaveData>(json, PersistenceJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to deserialize NpcSaveData from JSON.");
}

/// <summary>
/// Data transfer object for player state and inventory (player.json).
/// </summary>
public sealed class PlayerSaveData
{
    public int Version { get; set; } = 1;
    public Vec3 Position { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public bool IsFlying { get; set; }
    public int SelectedHotbarSlot { get; set; }
    public List<InventorySlotDto> Inventory { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, PersistenceJsonOptions.Default);

    public static PlayerSaveData FromJson(string json) =>
        JsonSerializer.Deserialize<PlayerSaveData>(json, PersistenceJsonOptions.Default)
        ?? throw new InvalidOperationException("Failed to deserialize PlayerSaveData from JSON.");
}
