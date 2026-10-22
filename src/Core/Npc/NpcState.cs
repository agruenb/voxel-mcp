namespace VoxelKing.Core.Npc;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Npc.Perception;

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
    /// Whether the NPC is currently standing on a solid surface.
    /// </summary>
    public bool IsOnGround { get; set; }

    /// <summary>
    /// Whether the NPC collided horizontally with an obstacle during the last physics tick.
    /// </summary>
    public bool IsOnWall { get; set; }

    /// <summary>
    /// Horizontal bounding box width/depth in meters.
    /// </summary>
    public float Width { get; set; } = 0.6f;

    /// <summary>
    /// Vertical bounding box height in meters.
    /// </summary>
    public float Height { get; set; } = 1.8f;

    /// <summary>
    /// Maximum step-up height in meters for automated step climbing.
    /// </summary>
    public float StepHeight { get; set; } = 1.1f;

    /// <summary>
    /// Upward velocity impulse applied when jumping.
    /// </summary>
    public float JumpVelocity { get; set; } = 7.5f;

    /// <summary>
    /// Active primary action currently being executed by this NPC, or null if idle.
    /// </summary>
    public IAction? CurrentAction { get; set; }

    /// <summary>
    /// Scoped spatial perception query interface for this NPC.
    /// </summary>
    public INpcPerception? Perception { get; set; }

    /// <summary>
    /// Active path follower controlling navigation for this NPC, or null if idle.
    /// </summary>
    public IPathFollower? PathFollower { get; set; }

    /// <summary>
    /// Gets the remaining path waypoints being navigated, or null if no active path.
    /// </summary>
    public IReadOnlyList<Vec3i>? ActivePath => PathFollower?.RemainingWaypoints;

    /// <summary>
    /// Simulation timestamp when this NPC last executed a speech action, used for rate limiting.
    /// </summary>
    public float LastSayTime { get; set; } = -100.0f;

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

    /// <summary>
    /// Gets the current axis-aligned bounding box of the NPC based on its feet position.
    /// </summary>
    public Aabb GetBoundingBox() => Aabb.FromFeet(Position, Width, Height);

    /// <summary>
    /// Makes the NPC jump if currently on the ground, setting upward velocity.
    /// </summary>
    public bool Jump()
    {
        if (IsOnGround)
        {
            Velocity = Velocity with { Y = JumpVelocity };
            IsOnGround = false;
            return true;
        }

        return false;
    }

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
