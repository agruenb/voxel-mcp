namespace VoxelKing.Core.Tests.Npc;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;
using Xunit;

public class KinematicMoverTests
{
    private static (VoxelWorld world, IBlockRegistry blocks) CreateTestWorld()
    {
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks);
        return (world, blocks);
    }

    [Fact]
    public void Npc_Falls_Under_Gravity_And_Lands_On_Terrain()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Build flat floor at Y = 0 (top surface at Y = 1.0)
        for (int x = -5; x <= 5; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        var npc = new NpcState(new EntityId(1), "Faller", new Vec3(0f, 4.0f, 0f));
        Assert.False(npc.IsOnGround);

        // Step physics forward 1.0 second (60 ticks of 1/60s)
        for (int i = 0; i < 60; i++)
        {
            KinematicMover.Move(npc, world, 1f / 60f, gravity: 20f);
        }

        // Must land cleanly at Y = 1.0f on top of the block
        Assert.True(npc.IsOnGround);
        Assert.Equal(1.0f, npc.Position.Y, 2);
        Assert.Equal(0f, npc.Velocity.Y);
    }

    [Fact]
    public void Npc_Stopped_By_Two_Block_High_Wall()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Floor at Y = 0
        for (int x = -5; x <= 5; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // 2-block high wall at X = 2 (Y = 1 and Y = 2)
        for (int z = -2; z <= 2; z++)
        {
            world.SetBlock(2, 1, z, stoneId);
            world.SetBlock(2, 2, z, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "Walker", new Vec3(0.5f, 1.0f, 0f))
        {
            IsOnGround = true,
            Velocity = new Vec3(4.0f, 0f, 0f) // Moving towards +X
        };

        // Step physics forward 30 ticks (0.5s)
        for (int i = 0; i < 30; i++)
        {
            KinematicMover.Move(npc, world, 1f / 60f);
        }

        // Wall begins at X = 2.0. With NPC half-width 0.3m, max position X is 2.0 - 0.3 = 1.7m.
        Assert.True(npc.Position.X <= 1.701f, $"NPC penetrated wall! Position X was {npc.Position.X}");
        Assert.True(npc.IsOnWall);
        Assert.True(npc.IsOnGround);
        Assert.Equal(1.0f, npc.Position.Y, 2);
    }

    [Fact]
    public void Npc_Steps_Up_One_Block_Step_Without_Jumping()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Ground at Y = 0
        for (int x = -5; x <= 5; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // 1-block high step at X = 2 (Y = 1), top surface at Y = 2.0f
        for (int z = -2; z <= 2; z++)
        {
            world.SetBlock(2, 1, z, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "Climber", new Vec3(1.0f, 1.0f, 0f))
        {
            IsOnGround = true,
            StepHeight = 1.1f,
            Velocity = new Vec3(3.0f, 0f, 0f) // Walk into the step
        };

        // Run 30 ticks
        for (int i = 0; i < 30; i++)
        {
            KinematicMover.Move(npc, world, 1f / 60f);
        }

        // The NPC should have smoothly stepped up onto the 1-block step (Y = 2.0f) and continued past X = 2.0
        Assert.True(npc.Position.X > 2.0f, $"NPC did not step over! Position X was {npc.Position.X}");
        Assert.Equal(2.0f, npc.Position.Y, 2);
        Assert.True(npc.IsOnGround);
    }

    [Fact]
    public void Npc_Jump_Clears_One_Block_Obstacle()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Floor at Y = 0
        for (int x = -5; x <= 5; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // 1-block obstacle at X = 2
        for (int z = -2; z <= 2; z++)
        {
            world.SetBlock(2, 1, z, stoneId);
        }

        var npc = new NpcState(new EntityId(1), "Jumper", new Vec3(1.0f, 1.0f, 0f))
        {
            IsOnGround = true,
            StepHeight = 0f, // Disable auto-step to test pure jumping
            Velocity = new Vec3(3.0f, 0f, 0f)
        };

        // Jump!
        bool jumped = npc.Jump();
        Assert.True(jumped);
        Assert.Equal(npc.JumpVelocity, npc.Velocity.Y);

        // Run 30 ticks
        for (int i = 0; i < 30; i++)
        {
            KinematicMover.Move(npc, world, 1f / 60f, gravity: 20f);
        }

        // NPC jumped over the obstacle
        Assert.True(npc.Position.X > 2.0f, $"NPC did not clear obstacle! Position X: {npc.Position.X}");
    }

    [Fact]
    public void Npc_Falls_Off_Ledge()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Elevated platform at Y = 5 for X <= 2
        for (int x = -5; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                world.SetBlock(x, 5, z, stoneId);
            }
        }

        // Ground far below at Y = 0
        for (int x = -5; x <= 10; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        var npc = new NpcState(new EntityId(1), "LedgeWalker", new Vec3(1.5f, 6.0f, 0f))
        {
            IsOnGround = true,
            Velocity = new Vec3(4.0f, 0f, 0f) // Walk off the ledge (+X)
        };

        // Run 30 ticks
        for (int i = 0; i < 30; i++)
        {
            KinematicMover.Move(npc, world, 1f / 60f, gravity: 20f);
        }

        // NPC walked past X = 2.0 and is now falling (Y < 6.0)
        Assert.True(npc.Position.X > 2.0f);
        Assert.True(npc.Position.Y < 6.0f);
    }
}
