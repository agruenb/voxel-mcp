namespace VoxelKing.Core.Tests.Npc;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcStateTests
{
    [Fact]
    public void NpcState_Initializes_With_Required_Properties()
    {
        var id = new EntityId(10);
        var home = new Vec3(10f, 64f, 10f);
        var pos = new Vec3(12f, 64f, 15f);
        var customInv = new Inventory(16);

        var npc = new NpcState(
            id: id,
            name: "Lumberjack Bob",
            position: pos,
            profile: "Friendly woodcutter",
            home: home,
            inventory: customInv,
            health: 80f,
            facing: 1.57f
        );

        Assert.Equal(id, npc.Id);
        Assert.Equal(EntityKind.Npc, npc.Kind);
        Assert.Equal("Lumberjack Bob", npc.Name);
        Assert.Equal("Friendly woodcutter", npc.Profile);
        Assert.Equal(pos, npc.Position);
        Assert.Equal(Vec3.Zero, npc.Velocity);
        Assert.Equal(home, npc.Home);
        Assert.Same(customInv, npc.Inventory);
        Assert.Equal(80f, npc.Health);
        Assert.Equal(1.57f, npc.Facing);
        Assert.True(npc.IsActive);
        Assert.False(npc.IsDead);
    }

    [Fact]
    public void FacingDirection_Calculates_Forward_Vector_And_Sets_Yaw()
    {
        var npc = new NpcState(new EntityId(1), "Alice", Vec3.Zero);

        // Facing = 0 -> Forward is (0, 0, -1)
        npc.Facing = 0f;
        var dir0 = npc.GetFacingDirection();
        Assert.True(MathF.Abs(dir0.X) < 1e-4f);
        Assert.True(MathF.Abs(dir0.Y) < 1e-4f);
        Assert.True(MathF.Abs(dir0.Z - (-1f)) < 1e-4f);

        // Set facing direction toward +X (1, 0, 0)
        npc.SetFacingDirection(new Vec3(1f, 0f, 0f));
        var dirX = npc.GetFacingDirection();
        Assert.True(MathF.Abs(dirX.X - 1f) < 1e-4f);
        Assert.True(MathF.Abs(dirX.Z) < 1e-4f);
    }

    [Fact]
    public void Damage_And_Healing_Modify_Health_Within_Bounds()
    {
        var npc = new NpcState(new EntityId(1), "Bob", Vec3.Zero, health: 100f);

        npc.TakeDamage(30f);
        Assert.Equal(70f, npc.Health);
        Assert.False(npc.IsDead);

        npc.Heal(20f);
        Assert.Equal(90f, npc.Health);

        // Clamps at MaxHealth
        npc.Heal(50f);
        Assert.Equal(100f, npc.Health);

        // Lethal damage clamps at 0
        npc.TakeDamage(150f);
        Assert.Equal(0f, npc.Health);
        Assert.True(npc.IsDead);
    }
}
