namespace VoxelKing.Core.Npc;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Authoritative simulation state for an individual NPC in the world.
/// </summary>
public sealed class NpcState : IEntity
{
    public const float DefaultMaxHealth = 100f;

    public EntityId Id { get; init; }
    public EntityKind Kind => EntityKind.Npc;
    public string Name { get; set; }
    public string Profile { get; set; }
    public Vec3 Position { get; set; }
    public Vec3 Velocity { get; set; }

    /// <summary>
    /// Facing direction represented as a yaw angle in radians around the Y axis.
    /// Yaw 0 points along -Z (standard forward).
    /// </summary>
    public float Facing { get; set; }

    public float Health { get; set; }
    public float MaxHealth { get; set; } = DefaultMaxHealth;
    public IInventory Inventory { get; }
    public Vec3 Home { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Entity ID of the instructor who assigned the current task, or null.
    /// </summary>
    public EntityId? InstructorId { get; set; }

    /// <summary>
    /// Current distance-based simulation level of detail.
    /// </summary>
    public NpcLodTier LodTier { get; internal set; } = NpcLodTier.Normal;

    /// <summary>
    /// Total number of brain ticks executed for this NPC.
    /// </summary>
    public int BrainTickCount { get; internal set; }

    /// <summary>
    /// Elapsed seconds since this NPC's brain last ticked.
    /// </summary>
    public float TimeSinceLastBrainTick { get; internal set; }

    public bool IsDead => Health <= 0f;

    public NpcState(
        EntityId id,
        string name,
        Vec3 position,
        string profile = "",
        Vec3? home = null,
        IInventory? inventory = null,
        float health = DefaultMaxHealth,
        float facing = 0f)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("NPC name cannot be null or whitespace.", nameof(name));
        }

        Id = id;
        Name = name;
        Profile = profile;
        Position = position;
        Velocity = Vec3.Zero;
        Home = home ?? position;
        Inventory = inventory ?? new Inventory(36);
        Health = Math.Clamp(health, 0f, MaxHealth);
        Facing = facing;
    }

    /// <summary>
    /// Computes the normalized horizontal forward vector from the current facing yaw.
    /// </summary>
    public Vec3 GetFacingDirection()
    {
        return new Vec3(-MathF.Sin(Facing), 0f, -MathF.Cos(Facing)).Normalized();
    }

    /// <summary>
    /// Sets the facing yaw to look toward the specified direction vector.
    /// </summary>
    public void SetFacingDirection(Vec3 direction)
    {
        if (direction.LengthSquared() <= 1e-6f)
        {
            return;
        }

        Facing = MathF.Atan2(-direction.X, -direction.Z);
    }

    /// <summary>
    /// Reduces health by the given damage amount, clamped at 0.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        Health = Math.Max(0f, Health - amount);
    }

    /// <summary>
    /// Increases health by the given heal amount, clamped at MaxHealth.
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        Health = Math.Min(MaxHealth, Health + amount);
    }
}
