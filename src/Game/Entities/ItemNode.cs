namespace VoxelKing.Game.Entities;

using System;
using Godot;
using VoxelKing.Core.Entities;

/// <summary>
/// Visual representation of a dropped item entity in the Godot scene tree.
/// Renders as a miniature voxel block resting directly flat on the ground.
/// Does not hover or bob in mid-air. Contains no simulation logic.
/// </summary>
public partial class ItemNode : Node3D
{
    public ItemEntity? Entity { get; private set; }
    public MeshInstance3D MeshInstance { get; private set; } = null!;

    public void BindEntity(ItemEntity entity, Mesh mesh, Material material)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));

        GlobalPosition = new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);

        // A stable fixed rotation so dropped items rest naturally on the floor without continuously spinning
        float yaw = ((entity.Id.Value * 37) % 360) * (Mathf.Pi / 180.0f);
        Rotation = new Vector3(0, yaw, 0);

        MeshInstance = new MeshInstance3D
        {
            Name = "MeshInstance",
            Mesh = mesh,
            MaterialOverride = material
        };
        AddChild(MeshInstance);
    }

    public override void _Process(double delta)
    {
        if (Entity == null)
        {
            return;
        }

        Vector3 targetPos = new(Entity.Position.X, Entity.Position.Y, Entity.Position.Z);
        float dist = GlobalPosition.DistanceTo(targetPos);

        if (dist > 3.0f || dist < 0.001f)
        {
            GlobalPosition = targetPos;
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(targetPos, (float)(25.0 * delta));
        }
    }
}
