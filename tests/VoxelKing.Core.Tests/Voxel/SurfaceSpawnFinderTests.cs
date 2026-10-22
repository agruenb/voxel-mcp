namespace VoxelKing.Core.Tests.Voxel;

using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class SurfaceSpawnFinderTests
{
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();

    [Fact]
    public void FindSurfaceSpawn_ReturnsPositionAboveWaterLevel()
    {
        var generator = new TerrainGenerator(1337, _registry);
        var spawnPos = SurfaceSpawnFinder.FindSurfaceSpawn(generator, 0, 0);

        Assert.True(spawnPos.Y > generator.WaterLevel, $"Spawn Y {spawnPos.Y} should be above water level {generator.WaterLevel}");

        int blockX = (int)MathF.Floor(spawnPos.X);
        int blockZ = (int)MathF.Floor(spawnPos.Z);
        int expectedHeight = generator.GetHeight(blockX, blockZ);

        Assert.Equal(expectedHeight + 1.0f, spawnPos.Y);
    }

    [Fact]
    public void FindSurfaceSpawn_DoesNotSpawnInTreeTrunk()
    {
        var generator = new TerrainGenerator(1337, _registry);

        // Find a coordinate known to have a tree
        int foundTreeX = 0;
        int foundTreeZ = 0;
        bool foundTree = false;

        for (int x = -32; x <= 32 && !foundTree; x++)
        {
            for (int z = -32; z <= 32 && !foundTree; z++)
            {
                if (generator.HasTreeTrunk(x, z))
                {
                    foundTreeX = x;
                    foundTreeZ = z;
                    foundTree = true;
                }
            }
        }

        Assert.True(foundTree, "Should find at least one tree in 64x64 area");

        // Ask for spawn directly at tree coordinates
        var spawnPos = SurfaceSpawnFinder.FindSurfaceSpawn(generator, foundTreeX, foundTreeZ);

        int spawnBlockX = (int)MathF.Floor(spawnPos.X);
        int spawnBlockZ = (int)MathF.Floor(spawnPos.Z);

        Assert.False(generator.HasTreeTrunk(spawnBlockX, spawnBlockZ),
            $"Spawn coordinate ({spawnBlockX}, {spawnBlockZ}) must not be a tree trunk");
    }

    [Fact]
    public void FindSurfaceSpawn_IsDeterministicForSameSeed()
    {
        var genA = new TerrainGenerator(42, _registry);
        var genB = new TerrainGenerator(42, _registry);

        var spawnA = SurfaceSpawnFinder.FindSurfaceSpawn(genA, 10, -5);
        var spawnB = SurfaceSpawnFinder.FindSurfaceSpawn(genB, 10, -5);

        Assert.Equal(spawnA.X, spawnB.X);
        Assert.Equal(spawnA.Y, spawnB.Y);
        Assert.Equal(spawnA.Z, spawnB.Z);
    }

    [Fact]
    public void FindSurfaceSpawn_WithVoxelWorld_FindsClearSpace()
    {
        var eventBus = new EventBus();
        var world = new VoxelWorld(_registry, eventBus);
        var generator = new TerrainGenerator(1337, _registry);

        // Populate chunk at 0, 2, 0 (y: 64..95)
        var chunk = generator.GenerateChunk(new Vec3i(0, 2, 0));
        world.AddOrUpdateChunk(chunk);

        var spawn = SurfaceSpawnFinder.FindSurfaceSpawn(generator, world, 0, 0);

        int bx = (int)MathF.Floor(spawn.X);
        int by = (int)MathF.Floor(spawn.Y);
        int bz = (int)MathF.Floor(spawn.Z);

        // Feet and head should not be solid blocks
        ushort feetBlock = world.GetBlock(bx, by, bz);
        ushort headBlock = world.GetBlock(bx, by + 1, bz);

        Assert.False(_registry.GetDefinition(feetBlock).IsSolid, "Feet block should not be solid");
        Assert.False(_registry.GetDefinition(headBlock).IsSolid, "Head block should not be solid");
    }
}
