namespace VoxelKing.Core.Entities;

using System;
using System.Collections.Generic;

/// <summary>
/// Slot-based inventory implementation supporting stacking, slot queries, and cross-inventory item transfers.
/// </summary>
public sealed class Inventory : IInventory
{
    private readonly ItemStack?[] _slots;

    public int SlotCount => _slots.Length;

    public int FreeSlots
    {
        get
        {
            int free = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i]!.IsEmpty) free++;
            }
            return free;
        }
    }

    public bool IsFull => FreeSlots == 0;

    public bool IsEmpty => FreeSlots == _slots.Length;

    public event Action? Changed;

    public Inventory(int slotCount = 36)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Slot count must be greater than zero.");
        }

        _slots = new ItemStack?[slotCount];
    }

    public ItemStack? GetSlot(int slot)
    {
        if (slot < 0 || slot >= _slots.Length) return null;
        var s = _slots[slot];
        return (s == null || s.IsEmpty) ? null : s;
    }

    public void SetSlot(int slot, ItemStack? item)
    {
        if (slot < 0 || slot >= _slots.Length) return;

        if (item == null || item.IsEmpty)
        {
            _slots[slot] = null;
        }
        else
        {
            _slots[slot] = item;
        }

        Changed?.Invoke();
    }

    public bool AddItem(string itemId, int count, out int remainder)
    {
        if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
        {
            remainder = count;
            return false;
        }

        var stack = new ItemStack(itemId, count);
        return AddItem(stack, out remainder);
    }

    public bool AddItem(ItemStack stack, out int remainder)
    {
        ArgumentNullException.ThrowIfNull(stack);

        int toAdd = stack.Count;
        if (toAdd <= 0)
        {
            remainder = 0;
            return true;
        }

        // 1. Try to merge into existing non-full stacks
        for (int i = 0; i < _slots.Length && toAdd > 0; i++)
        {
            var cur = _slots[i];
            if (cur != null && cur.ItemId.Equals(stack.ItemId, StringComparison.OrdinalIgnoreCase) && cur.Count < cur.MaxStack)
            {
                int space = cur.MaxStack - cur.Count;
                int added = Math.Min(space, toAdd);
                cur.Count += added;
                toAdd -= added;
            }
        }

        // 2. Place remainder into empty slots
        for (int i = 0; i < _slots.Length && toAdd > 0; i++)
        {
            if (_slots[i] == null || _slots[i]!.IsEmpty)
            {
                int added = Math.Min(stack.MaxStack, toAdd);
                _slots[i] = new ItemStack(stack.ItemId, added, stack.MaxStack);
                toAdd -= added;
            }
        }

        remainder = toAdd;
        bool addedAny = remainder < stack.Count;
        if (addedAny)
        {
            Changed?.Invoke();
        }

        return remainder == 0;
    }

    public bool RemoveItem(string itemId, int count)
    {
        if (string.IsNullOrWhiteSpace(itemId) || count <= 0) return false;

        int available = CountItem(itemId);
        if (available < count) return false;

        int toRemove = count;
        for (int i = 0; i < _slots.Length && toRemove > 0; i++)
        {
            var cur = _slots[i];
            if (cur != null && cur.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
            {
                int removed = Math.Min(cur.Count, toRemove);
                cur.Count -= removed;
                toRemove -= removed;

                if (cur.Count <= 0)
                {
                    _slots[i] = null;
                }
            }
        }

        Changed?.Invoke();
        return true;
    }

    public bool RemoveAt(int slot, int count)
    {
        if (slot < 0 || slot >= _slots.Length || count <= 0) return false;

        var cur = _slots[slot];
        if (cur == null || cur.IsEmpty) return false;

        int removed = Math.Min(cur.Count, count);
        cur.Count -= removed;

        if (cur.Count <= 0)
        {
            _slots[slot] = null;
        }

        Changed?.Invoke();
        return true;
    }

    public int CountItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;

        int total = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            var cur = _slots[i];
            if (cur != null && cur.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
            {
                total += cur.Count;
            }
        }
        return total;
    }

    public IReadOnlyDictionary<string, int> GetAllCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _slots.Length; i++)
        {
            var cur = _slots[i];
            if (cur != null && !cur.IsEmpty)
            {
                counts[cur.ItemId] = counts.GetValueOrDefault(cur.ItemId, 0) + cur.Count;
            }
        }
        return counts;
    }

    public void Clear()
    {
        Array.Clear(_slots, 0, _slots.Length);
        Changed?.Invoke();
    }

    /// <summary>
    /// Transfers items from a source inventory slot into any available slot in the target inventory.
    /// Returns the number of items successfully transferred.
    /// </summary>
    public static int Transfer(IInventory source, int sourceSlot, IInventory target, int count = -1)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var srcStack = source.GetSlot(sourceSlot);
        if (srcStack == null || srcStack.IsEmpty) return 0;

        int amount = count < 0 ? srcStack.Count : Math.Min(count, srcStack.Count);
        if (amount <= 0) return 0;

        var transferStack = new ItemStack(srcStack.ItemId, amount, srcStack.MaxStack);
        target.AddItem(transferStack, out int remainder);

        int transferred = amount - remainder;
        if (transferred > 0)
        {
            source.RemoveAt(sourceSlot, transferred);
        }

        return transferred;
    }

    /// <summary>
    /// Transfers or swaps items between a specific source slot and a specific target slot.
    /// </summary>
    public static bool TransferSlotToSlot(IInventory source, int sourceSlot, IInventory target, int targetSlot, int count = -1)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var src = source.GetSlot(sourceSlot);
        if (src == null || src.IsEmpty) return false;

        var dst = target.GetSlot(targetSlot);
        int amount = count < 0 ? src.Count : Math.Min(count, src.Count);

        // Target slot is empty
        if (dst == null || dst.IsEmpty)
        {
            var placed = new ItemStack(src.ItemId, amount, src.MaxStack);
            source.RemoveAt(sourceSlot, amount);
            target.SetSlot(targetSlot, placed);
            return true;
        }

        // Target slot has same item type -> stack them
        if (dst.ItemId.Equals(src.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            int space = dst.MaxStack - dst.Count;
            if (space <= 0) return false;

            int toMove = Math.Min(space, amount);
            dst.Count += toMove;
            source.RemoveAt(sourceSlot, toMove);
            target.SetSlot(targetSlot, dst);
            return true;
        }

        // Different item types -> swap slots if entire source stack is moved
        if (amount == src.Count)
        {
            source.SetSlot(sourceSlot, dst);
            target.SetSlot(targetSlot, src);
            return true;
        }

        return false;
    }
}
