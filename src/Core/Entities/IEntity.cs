namespace VoxelKing.Core.Entities;

using VoxelKing.Core.Common;

/// <summary>
/// Common contract for all spatial entities tracked by the entity registry.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Unique entity identifier.
    /// </summary>
    EntityId Id { get; }

    /// <summary>
    /// Classification kind of the entity.
    /// </summary>
    EntityKind Kind { get; }

    /// <summary>
    /// Human-readable display or type name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Continuous 3D world position (e.g. feet position).
    /// </summary>
    Vec3 Position { get; set; }

    /// <summary>
    /// Current continuous velocity in m/s.
    /// </summary>
    Vec3 Velocity { get; set; }

    /// <summary>
    /// Whether the entity is currently active in the simulation.
    /// </summary>
    bool IsActive { get; }
}
