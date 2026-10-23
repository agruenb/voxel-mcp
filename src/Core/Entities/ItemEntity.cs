namespace VoxelKing.Core.Entities;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Represents a dropped item stack in the world that can be picked up by players or NPCs.
/// </summary>
public sealed class ItemEntity : IEntity
{
    public EntityId Id { get; init; }
    public EntityKind Kind => EntityKind.Item;
    public string Name => Stack.ItemId;
    public Vec3 Position { get; set; }
    public Vec3 Velocity { get; set; }
    public ItemStack Stack { get; }
    public float PickupCooldown { get; set; }
    public bool IsCollected { get; private set; }
    public bool IsActive => !IsCollected && !Stack.IsEmpty;
    public bool CanPickup => IsActive && PickupCooldown <= 0f;

    public ItemEntity(
        EntityId id,
        ItemStack stack,
        Vec3 position,
        Vec3? velocity = null,
        float pickupCooldown = 0.5f)
    {
        ArgumentNullException.ThrowIfNull(stack);
        if (stack.IsEmpty)
        {
            throw new ArgumentException("Cannot create an item entity from an empty item stack.", nameof(stack));
        }

        Id = id;
        Stack = stack;
        Position = position;
        Velocity = velocity ?? Vec3.Zero;
        PickupCooldown = Math.Max(0f, pickupCooldown);
    }

    /// <summary>
    /// Marks this item entity as collected.
    /// </summary>
    public void MarkCollected()
    {
        IsCollected = true;
    }

    /// <summary>
    /// Attempts to transfer items from this entity into the target inventory.
    /// If all items are absorbed, marks the entity as collected.
    /// </summary>
    public bool TryPickup(IInventory inventory, out int countPickedUp)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        countPickedUp = 0;

        if (!CanPickup)
        {
            return false;
        }

        int initialCount = Stack.Count;
        bool added = inventory.AddItem(Stack, out int remainder);
        countPickedUp = initialCount - remainder;
        Stack.Count = remainder;

        if (Stack.IsEmpty)
        {
            MarkCollected();
        }

        return countPickedUp > 0;
    }

    /// <summary>
    /// Updates item physics (gravity, landing on solid blocks, wall collisions) and decrements pickup cooldown.
    /// </summary>
    public void TickPhysics(float dt, IVoxelWorld? world = null, float gravity = 20.0f)
    {
        if (IsCollected)
        {
            return;
        }

        if (PickupCooldown > 0f)
        {
            PickupCooldown = Math.Max(0f, PickupCooldown - dt);
        }

        // Apply gravity to velocity
        Velocity = Velocity with { Y = Velocity.Y - (gravity * dt) };
        Vec3 nextPos = Position + (Velocity * dt);

        if (world != null)
        {
            Vec3i blockPos = nextPos.FloorToInt();
            ushort blockId = world.GetBlock(blockPos);
            if (world.BlockRegistry.TryGetDefinition(blockId, out var def) && def.IsSolid)
            {
                // If coming down from above the solid block, land on top of the block
                if (Position.Y >= blockPos.Y + 1.0f)
                {
                    nextPos = new Vec3(nextPos.X, blockPos.Y + 1.0f, nextPos.Z);
                    Velocity = new Vec3(Velocity.X * 0.7f, 0f, Velocity.Z * 0.7f);
                    if (MathF.Abs(Velocity.X) < 0.05f && MathF.Abs(Velocity.Z) < 0.05f)
                    {
                        Velocity = Vec3.Zero;
                    }
                }
                else
                {
                    // Collided with a wall or ceiling obstacle horizontally: stop horizontal velocity
                    nextPos = new Vec3(Position.X, Math.Max(blockPos.Y + 1.0f, nextPos.Y), Position.Z);
                    Velocity = new Vec3(0f, Velocity.Y <= 0f ? 0f : -gravity * dt, 0f);
                }
            }
        }

        Position = nextPos;
    }
}
