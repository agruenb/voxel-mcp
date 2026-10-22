namespace VoxelKing.Core.Entities;

using VoxelKing.Core.Common;

/// <summary>
/// Core representation of a player entity registered in the world.
/// </summary>
public sealed class PlayerEntity : IEntity
{
    public EntityId Id { get; init; }
    public EntityKind Kind => EntityKind.Player;
    public string Name { get; set; }
    public Vec3 Position { get; set; }
    public Vec3 Velocity { get; set; }
    public float Health { get; set; }
    public IInventory? Inventory { get; set; }
    public bool IsActive { get; set; } = true;

    public PlayerEntity(
        EntityId id,
        string name = "Player",
        Vec3? position = null,
        IInventory? inventory = null,
        float health = 100f)
    {
        Id = id;
        Name = name;
        Position = position ?? Vec3.Zero;
        Velocity = Vec3.Zero;
        Inventory = inventory;
        Health = health;
    }
}
