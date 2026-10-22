namespace VoxelKing.Core.Entities;

using System;

/// <summary>
/// Represents a stack of identical items with an ID, quantity, and maximum stack limit.
/// </summary>
public sealed class ItemStack
{
    public const int DefaultMaxStack = 64;

    public string ItemId { get; init; }
    public int Count { get; set; }
    public int MaxStack { get; init; }

    public bool IsEmpty => Count <= 0;

    public ItemStack(string itemId, int count = 1, int maxStack = DefaultMaxStack)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));
        }

        ItemId = itemId;
        Count = Math.Max(0, count);
        MaxStack = maxStack > 0 ? maxStack : DefaultMaxStack;
    }

    public ItemStack Copy() => new(ItemId, Count, MaxStack);
}
