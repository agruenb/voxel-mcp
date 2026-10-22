namespace VoxelKing.Core.Tests.Navigation;

using System;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Voxel;
using Xunit;

public class WalkabilityTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks) CreateTestWorld()
    {
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks);
        return (world, blocks);
    }

    [Fact]
    public void IsWalkable_ReturnsTrue_WhenGroundSolid_AndFeetHeadAir()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Ground at Y = 0
        world.SetBlock(0, 0, 0, stoneId);

        // Standing at Y = 1: feet at Y=1 (air), head at Y=2 (air), ground at Y=0 (solid)
        var pos = new Vec3i(0, 1, 0);
        Assert.True(Walkability.IsWalkable(world, pos));
        Assert.True(world.IsWalkable(pos));
    }

    [Fact]
    public void IsWalkable_ReturnsFalse_WhenGroundIsNotSolid()
    {
        var (world, _) = CreateTestWorld();

        // All air
        var pos = new Vec3i(0, 1, 0);
        Assert.False(Walkability.IsWalkable(world, pos));
    }

    [Fact]
    public void IsWalkable_ReturnsFalse_WhenFeetIsSolid()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(0, 1, 0, stoneId); // solid block at feet

        var pos = new Vec3i(0, 1, 0);
        Assert.False(Walkability.IsWalkable(world, pos));
    }

    [Fact]
    public void IsWalkable_ReturnsFalse_WhenHeadIsSolid()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(0, 2, 0, stoneId); // solid block at head (low ceiling)

        var pos = new Vec3i(0, 1, 0);
        Assert.False(Walkability.IsWalkable(world, pos));
    }

    [Fact]
    public void IsWalkable_HandlesWater_AccordingToOptions()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");
        ushort waterId = blocks.GetId("water");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(0, 1, 0, waterId); // water at feet

        var pos = new Vec3i(0, 1, 0);

        // By default, water is non-solid, so cell is walkable
        Assert.True(Walkability.IsWalkable(world, pos, new WalkabilityOptions { PreventWater = false }));

        // With PreventWater = true, cell is not walkable
        Assert.False(Walkability.IsWalkable(world, pos, new WalkabilityOptions { PreventWater = true }));
    }

    [Fact]
    public void IsWalkable_ReturnsFalse_WhenGroundIsWater()
    {
        var (world, blocks) = CreateTestWorld();
        ushort waterId = blocks.GetId("water");

        // Water is non-solid, so entity cannot stand on top of water
        world.SetBlock(0, 0, 0, waterId);

        var pos = new Vec3i(0, 1, 0);
        Assert.False(Walkability.IsWalkable(world, pos));
    }

    [Fact]
    public void Neighbours_FlatGround_Returns8Neighbors()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // 3x3 flat floor at Y = 0
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();
        var withCost = Walkability.NeighboursWithCost(world, pos).ToList();

        Assert.Equal(8, neighbors.Count);
        Assert.Equal(8, withCost.Count);

        // 4 cardinal moves have distance 1.0 and DeltaY 0
        var cardinals = withCost.Where(n => !n.IsDiagonal).ToList();
        Assert.Equal(4, cardinals.Count);
        Assert.All(cardinals, c =>
        {
            Assert.Equal(1.0f, c.Cost, 3);
            Assert.Equal(0, c.DeltaY);
        });

        // 4 diagonal moves have distance sqrt(2) ≈ 1.414 and DeltaY 0
        var diagonals = withCost.Where(n => n.IsDiagonal).ToList();
        Assert.Equal(4, diagonals.Count);
        Assert.All(diagonals, d =>
        {
            Assert.Equal(MathF.Sqrt(2), d.Cost, 3);
            Assert.Equal(0, d.DeltaY);
        });
    }

    [Fact]
    public void Neighbours_LowCeiling_BlocksFlatMove()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Floor at Y = 0
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // Low ceiling at East: (1, 2, 0) is solid stone
        world.SetBlock(1, 2, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        // East (1, 1, 0) should be blocked because head at (1, 2, 0) is obstructed
        Assert.DoesNotContain(new Vec3i(1, 1, 0), neighbors);

        // Also, diagonals touching East (+X) must be blocked because adjacent East (1, 2, 0) is not clear
        Assert.DoesNotContain(new Vec3i(1, 1, -1), neighbors);
        Assert.DoesNotContain(new Vec3i(1, 1, 1), neighbors);

        // North, South, West and remaining diagonals should be valid
        Assert.Contains(new Vec3i(0, 1, -1), neighbors);
        Assert.Contains(new Vec3i(0, 1, 1), neighbors);
        Assert.Contains(new Vec3i(-1, 1, 0), neighbors);
        Assert.Contains(new Vec3i(-1, 1, -1), neighbors);
        Assert.Contains(new Vec3i(-1, 1, 1), neighbors);
    }

    [Fact]
    public void Neighbours_OneBlockStepUp_IsValid()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Floor at (0, 0, 0)
        world.SetBlock(0, 0, 0, stoneId);

        // 1-block step up at East: (1, 1, 0) is solid stone
        world.SetBlock(1, 1, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.NeighboursWithCost(world, pos).ToList();

        // Should contain step-up to (1, 2, 0)
        var stepUp = neighbors.FirstOrDefault(n => n.Destination == new Vec3i(1, 2, 0));
        Assert.Equal(new Vec3i(1, 2, 0), stepUp.Destination);
        Assert.Equal(1, stepUp.DeltaY);
        Assert.False(stepUp.IsDiagonal);
        Assert.Equal(MathF.Sqrt(2), stepUp.Cost, 3); // dx=1, dy=1 -> sqrt(2)
    }

    [Fact]
    public void Neighbours_StepUp_BlockedBySourceCeiling()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(1, 1, 0, stoneId); // step up at East

        // Ceiling right above agent at source: (0, 3, 0) is solid
        world.SetBlock(0, 3, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        // Stepping up requires room at (0, 3, 0) so agent doesn't bonk head while rising
        Assert.DoesNotContain(new Vec3i(1, 2, 0), neighbors);
    }

    [Fact]
    public void Neighbours_StepUp_BlockedByDestinationCeiling()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(1, 1, 0, stoneId); // step up at East

        // Ceiling above destination: (1, 3, 0) is solid
        world.SetBlock(1, 3, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        // Head at destination (1, 3, 0) blocked
        Assert.DoesNotContain(new Vec3i(1, 2, 0), neighbors);
    }

    [Fact]
    public void Neighbours_TwoBlockWall_BlocksStepUp()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        // 2-block high wall at East
        world.SetBlock(1, 1, 0, stoneId);
        world.SetBlock(1, 2, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        Assert.DoesNotContain(new Vec3i(1, 2, 0), neighbors);
        Assert.DoesNotContain(new Vec3i(1, 3, 0), neighbors);
    }

    [Fact]
    public void Neighbours_Drop1_IsValid()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Higher floor at X=0 (Y=3)
        world.SetBlock(0, 3, 0, stoneId);

        // Lower floor at X=1 (Y=2) -> drop 1 block
        world.SetBlock(1, 2, 0, stoneId);

        var pos = new Vec3i(0, 4, 0);
        var neighbors = Walkability.NeighboursWithCost(world, pos).ToList();

        var drop = neighbors.FirstOrDefault(n => n.Destination == new Vec3i(1, 3, 0));
        Assert.Equal(new Vec3i(1, 3, 0), drop.Destination);
        Assert.Equal(-1, drop.DeltaY);
        Assert.Equal(MathF.Sqrt(2), drop.Cost, 3);
    }

    [Fact]
    public void Neighbours_Drop2_IsValid()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 3, 0, stoneId);
        // Floor at Y=1 -> drop 2 blocks
        world.SetBlock(1, 1, 0, stoneId);

        var pos = new Vec3i(0, 4, 0);
        var neighbors = Walkability.NeighboursWithCost(world, pos).ToList();

        var drop = neighbors.FirstOrDefault(n => n.Destination == new Vec3i(1, 2, 0));
        Assert.Equal(new Vec3i(1, 2, 0), drop.Destination);
        Assert.Equal(-2, drop.DeltaY);
        Assert.Equal(MathF.Sqrt(5), drop.Cost, 3); // dx=1, dy=2 -> sqrt(5)
    }

    [Fact]
    public void Neighbours_Drop3_IsValid()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 3, 0, stoneId);
        // Floor at Y=0 -> drop 3 blocks
        world.SetBlock(1, 0, 0, stoneId);

        var pos = new Vec3i(0, 4, 0);
        var neighbors = Walkability.NeighboursWithCost(world, pos).ToList();

        var drop = neighbors.FirstOrDefault(n => n.Destination == new Vec3i(1, 1, 0));
        Assert.Equal(new Vec3i(1, 1, 0), drop.Destination);
        Assert.Equal(-3, drop.DeltaY);
        Assert.Equal(MathF.Sqrt(10), drop.Cost, 3); // dx=1, dy=3 -> sqrt(10)
    }

    [Fact]
    public void Neighbours_DropGreaterThan3_IsBlocked()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 4, 0, stoneId);
        // Floor 4 blocks down at Y=-1
        world.SetBlock(1, -1, 0, stoneId);

        var pos = new Vec3i(0, 5, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        // Drop of 4 is not allowed with default MaxDrop=3
        Assert.DoesNotContain(neighbors, n => n.X == 1);
    }

    [Fact]
    public void Neighbours_DropBlocked_ByExitOverhang()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 3, 0, stoneId);
        world.SetBlock(1, 1, 0, stoneId); // drop of 2

        // Overhang at neighbor column at head height: (1, 5, 0) is solid
        world.SetBlock(1, 5, 0, stoneId);

        var pos = new Vec3i(0, 4, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        // Cannot drop because exit clearance at (1, 5, 0) is blocked by overhang
        Assert.DoesNotContain(new Vec3i(1, 2, 0), neighbors);
    }

    [Fact]
    public void Neighbours_DiagonalCornerCutting_IsPreventedByObstacle()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // 3x3 floor at Y = 0
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                world.SetBlock(x, 0, z, stoneId);
            }
        }

        // Place a pillar obstacle at East: (1, 1, 0)
        world.SetBlock(1, 1, 0, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();

        // Cardinal East is blocked by the pillar
        Assert.DoesNotContain(new Vec3i(1, 1, 0), neighbors);

        // Diagonals adjacent to East (North-East and South-East) MUST be blocked
        Assert.DoesNotContain(new Vec3i(1, 1, -1), neighbors);
        Assert.DoesNotContain(new Vec3i(1, 1, 1), neighbors);

        // Other diagonals (North-West and South-West) are clear and must be present
        Assert.Contains(new Vec3i(-1, 1, -1), neighbors);
        Assert.Contains(new Vec3i(-1, 1, 1), neighbors);
    }

    [Fact]
    public void Neighbours_DiagonalStepUp_RequiresBothAdjacentCellsClear()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        // Step up at (1, 1, 1) -> solid floor at (1, 1, 1)
        world.SetBlock(1, 1, 1, stoneId);

        var pos = new Vec3i(0, 1, 0);
        var neighborsClear = Walkability.NeighboursWithCost(world, pos).ToList();

        // Clear case: diagonal step up is valid
        var stepUp = neighborsClear.FirstOrDefault(n => n.Destination == new Vec3i(1, 2, 1));
        Assert.Equal(new Vec3i(1, 2, 1), stepUp.Destination);
        Assert.Equal(1, stepUp.DeltaY);
        Assert.True(stepUp.IsDiagonal);
        Assert.Equal(MathF.Sqrt(3), stepUp.Cost, 3); // dx=1, dy=1, dz=1 -> sqrt(3)

        // Block adjacent cell (1, 2, 0) at step height
        world.SetBlock(1, 2, 0, stoneId);
        var neighborsBlocked = Walkability.Neighbours(world, pos).ToList();
        Assert.DoesNotContain(new Vec3i(1, 2, 1), neighborsBlocked);
    }

    [Fact]
    public void Neighbours_ReturnsEmpty_WhenSourceCellIsNotWalkable()
    {
        var (world, _) = CreateTestWorld();

        // Air everywhere: source (0, 1, 0) has no floor
        var pos = new Vec3i(0, 1, 0);
        var neighbors = Walkability.Neighbours(world, pos).ToList();
        Assert.Empty(neighbors);
    }

    [Fact]
    public void Surface_ReturnsHighestWalkableCell_InColumn()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        // Ground floor at Y = 0
        world.SetBlock(0, 0, 0, stoneId);

        // Floating platform at Y = 10
        world.SetBlock(0, 10, 0, stoneId);

        // Highest walkable cell in column (0, 0) should be on the platform (Y = 11)
        var surface = Walkability.Surface(world, 0, 0);
        Assert.NotNull(surface);
        Assert.Equal(new Vec3i(0, 11, 0), surface.Value);

        // If scanning below the platform (maxY = 9), it should find the floor at Y = 1
        var lowerSurface = Walkability.Surface(world, 0, 0, maxY: 9);
        Assert.NotNull(lowerSurface);
        Assert.Equal(new Vec3i(0, 1, 0), lowerSurface.Value);
    }

    [Fact]
    public void Surface_ReturnsNull_WhenNoWalkableCellInColumn()
    {
        var (world, _) = CreateTestWorld();

        // Completely empty world
        var surface = Walkability.Surface(world, 5, 5);
        Assert.Null(surface);
    }

    [Fact]
    public void Surface_HandlesWater_WithPreventWaterOption()
    {
        var (world, blocks) = CreateTestWorld();
        ushort sandId = blocks.GetId("sand");
        ushort waterId = blocks.GetId("water");

        // Lake bottom at Y = 10
        world.SetBlock(0, 10, 0, sandId);
        // Water at Y = 11 and Y = 12
        world.SetBlock(0, 11, 0, waterId);
        world.SetBlock(0, 12, 0, waterId);

        // Without water prevention: cell (0, 11, 0) has solid sand below, non-solid water at feet/head
        var submergedSurface = Walkability.Surface(world, 0, 0, options: new WalkabilityOptions { PreventWater = false });
        Assert.NotNull(submergedSurface);
        Assert.Equal(new Vec3i(0, 11, 0), submergedSurface.Value);

        // With water prevention: water is ignored, no dry surface
        var drySurface = Walkability.Surface(world, 0, 0, options: new WalkabilityOptions { PreventWater = true });
        Assert.Null(drySurface);
    }

    [Fact]
    public void WalkabilityInstance_MatchesStaticMethods()
    {
        var (world, blocks) = CreateTestWorld();
        ushort stoneId = blocks.GetId("stone");

        world.SetBlock(0, 0, 0, stoneId);
        world.SetBlock(1, 0, 0, stoneId);

        var walkability = new Walkability(world);
        var pos = new Vec3i(0, 1, 0);

        Assert.Equal(Walkability.IsWalkable(world, pos), walkability.IsWalkable(pos));
        Assert.Equal(Walkability.Neighbours(world, pos).ToList(), walkability.Neighbours(pos).ToList());
        Assert.Equal(Walkability.Surface(world, 0, 0), walkability.Surface(0, 0));
    }
}
