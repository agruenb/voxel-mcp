namespace VoxelKing.Core.Entities;

using System;
using System.Collections.Generic;

/// <summary>
/// Interface for slot-based item storage.
/// </summary>
public interface IInventory
{
    /// <summary>
    /// Total number of inventory slots.
    /// </summary>
    int SlotCount { get; }

    /// <summary>
    /// Number of empty slots.
    /// </summary>
    int FreeSlots { get; }

    /// <summary>
    /// Whether all slots contain items.
    /// </summary>
    bool IsFull { get; }

    /// <summary>
    /// Whether all slots are empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Raised whenever inventory slot contents are modified.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Retrieves the ItemStack at the specified slot index, or null if empty.
    /// </summary>
    ItemStack? GetSlot(int slot);

    /// <summary>
    /// Sets or clears the ItemStack at the specified slot index.
    /// </summary>
    void SetSlot(int slot, ItemStack? item);

    /// <summary>
    /// Attempts to add a given count of an item to the inventory, stacking with existing items where possible.
    /// </summary>
    bool AddItem(string itemId, int count, out int remainder);

    /// <summary>
    /// Attempts to add an item stack to the inventory, stacking where possible.
    /// </summary>
    bool AddItem(ItemStack stack, out int remainder);

    /// <summary>
    /// Removes a specified total count of an item from the inventory across all slots.
    /// Returns true if the full count was removed; false if insufficient items were present.
    /// </summary>
    bool RemoveItem(string itemId, int count);

    /// <summary>
    /// Removes up to count items from the specified slot.
    /// </summary>
    bool RemoveAt(int slot, int count);

    /// <summary>
    /// Counts total items of the given ID across all slots.
    /// </summary>
    int CountItem(string itemId);

    /// <summary>
    /// Returns a dictionary of item IDs to total counts in the inventory.
    /// </summary>
    IReadOnlyDictionary<string, int> GetAllCounts();

    /// <summary>
    /// Empties all slots in the inventory.
    /// </summary>
    void Clear();
}
