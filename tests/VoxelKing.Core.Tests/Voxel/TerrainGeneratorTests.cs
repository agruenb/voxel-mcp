namespace VoxelKing.Core.Tests.Voxel;

using System.Diagnostics;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;
using Xunit.Abstractions;

public class TerrainGeneratorTests
{
    private readonly ITestOutputHelper _output;
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();

    public TerrainGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalChunks()
    {
        var genA = new TerrainGenerator(1337, _registry);
        var genB = new TerrainGenerator(1337, _registry);

        var coord = new Vec3i(2, 2, -1);
        var chunkA = genA.GenerateChunk(coord);
        var chunkB = genB.GenerateChunk(coord);

        Assert.Equal(chunkA.Blocks.Length, chunkB.Blocks.Length);
        for (int i = 0; i < ChunkData.Volume; i++)
        {
            if (chunkA.Blocks[i] != chunkB.Blocks[i])
            {
                var local = ChunkData.GetLocalCoords(i);
                Assert.Fail($"Mismatch at local ({local.X}, {local.Y}, {local.Z}): A={chunkA.Blocks[i]} vs B={chunkB.Blocks[i]}");
            }
        }
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentChunks()
    {
        var genA = new TerrainGenerator(1337, _registry);
        var genB = new TerrainGenerator(9999, _registry);

        var coord = new Vec3i(0, 2, 0);
        var chunkA = genA.GenerateChunk(coord);
        var chunkB = genB.GenerateChunk(coord);

        bool foundDifference = false;
        for (int i = 0; i < ChunkData.Volume; i++)
        {
            if (chunkA.Blocks[i] != chunkB.Blocks[i])
            {
                foundDifference = true;
                break;
            }
        }

        Assert.True(foundDifference);
    }

    [Fact]
    public void SkyChunk_ContainsOnlyAir()
    {
        var generator = new TerrainGenerator(1337, _registry);
        var skyChunk = generator.GenerateChunk(new Vec3i(0, 4, 0)); // Y: 128..159

        for (int i = 0; i < ChunkData.Volume; i++)
        {
            Assert.Equal(_registry.AirId, skyChunk.Blocks[i]);
        }
    }

    [Fact]
    public void UndergroundChunk_ContainsOnlyStoneAndCoalOre()
    {
        var generator = new TerrainGenerator(1337, _registry);
        var deepChunk = generator.GenerateChunk(new Vec3i(0, 0, 0)); // Y: 0..31

        ushort stoneId = _registry.GetId("stone");
        ushort coalId = _registry.GetId("coal_ore");

        int stoneCount = 0;
        int coalCount = 0;

        for (int i = 0; i < ChunkData.Volume; i++)
        {
            ushort block = deepChunk.Blocks[i];
            Assert.True(block == stoneId || block == coalId, $"Unexpected block {block} found in deep underground chunk.");
            if (block == stoneId) stoneCount++;
            if (block == coalId) coalCount++;
        }

        Assert.True(stoneCount > 0);
        Assert.True(coalCount > 0, "Deep underground should contain coal ore veins.");
    }

    [Fact]
    public void SurfaceChunk_ContainsExpectedTerrainLayersAndFeatures()
    {
        var generator = new TerrainGenerator(1337, _registry);

        ushort airId = _registry.AirId;
        ushort grassId = _registry.GetId("grass");
        ushort dirtId = _registry.GetId("dirt");
        ushort stoneId = _registry.GetId("stone");
        ushort logId = _registry.GetId("oak_log");
        ushort leavesId = _registry.GetId("oak_leaves");

        bool hasAir = false;
        bool hasGrass = false;
        bool hasDirt = false;
        bool hasStone = false;
        bool hasTrees = false;

        // Sample surface and subsurface chunks (Y=1 and Y=2: Y 32..95)
        for (int cy = 1; cy <= 2; cy++)
        {
            var chunk = generator.GenerateChunk(new Vec3i(0, cy, 0));
            for (int i = 0; i < ChunkData.Volume; i++)
            {
                ushort block = chunk.Blocks[i];
                if (block == airId) hasAir = true;
                else if (block == grassId) hasGrass = true;
                else if (block == dirtId) hasDirt = true;
                else if (block == stoneId) hasStone = true;
                else if (block == logId || block == leavesId) hasTrees = true;
            }
        }

        Assert.True(hasAir, "Surface terrain should contain air above surface.");
        Assert.True(hasGrass, "Surface terrain should contain grass surface.");
        Assert.True(hasDirt, "Surface terrain should contain subsurface dirt.");
        Assert.True(hasStone, "Surface terrain should contain stone under dirt.");
        Assert.True(hasTrees, "Surface terrain should contain trees.");
    }

    [Fact]
    public void WaterAndSand_GeneratedAtOrBelowWaterLevel()
    {
        var generator = new TerrainGenerator(1337, _registry);

        // Search over multiple chunks around sea level to find water and beach sand
        ushort waterId = _registry.GetId("water");
        ushort sandId = _registry.GetId("sand");

        bool foundWater = false;
        bool foundSand = false;

        for (int cx = -3; cx <= 3 && (!foundWater || !foundSand); cx++)
        {
            for (int cz = -3; cz <= 3 && (!foundWater || !foundSand); cz++)
            {
                var chunk = generator.GenerateChunk(new Vec3i(cx, 1, cz)); // Y: 32..63 (water level 62 is here)
                for (int i = 0; i < ChunkData.Volume; i++)
                {
                    if (chunk.Blocks[i] == waterId) foundWater = true;
                    if (chunk.Blocks[i] == sandId) foundSand = true;
                }
            }
        }

        Assert.True(foundWater, "Water should be generated in low terrain at or below water level.");
        Assert.True(foundSand, "Sand should be generated near water level.");
    }

    [Fact]
    public void Benchmark_ChunkGenerationTimeUnder5ms()
    {
        var generator = new TerrainGenerator(1337, _registry);

        // Warmup JIT
        for (int i = 0; i < 6; i++)
        {
            generator.GenerateChunk(new Vec3i(i, 2, i));
        }

        // Measure generation of 27 chunks (3x3x3 grid)
        int chunkCount = 0;
        var sw = Stopwatch.StartNew();

        for (int cx = -1; cx <= 1; cx++)
        {
            for (int cy = 0; cy <= 2; cy++)
            {
                for (int cz = -1; cz <= 1; cz++)
                {
                    generator.GenerateChunk(new Vec3i(cx, cy, cz));
                    chunkCount++;
                }
            }
        }

        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMs = totalMs / chunkCount;

        _output.WriteLine($"[Benchmark] Generated {chunkCount} chunks in {totalMs:F2} ms (average {avgMs:F3} ms/chunk)");

        // Feature acceptance criterion: generation of a chunk < 5 ms
        Assert.True(avgMs < 5.0, $"Average chunk generation time ({avgMs:F3} ms) exceeded 5 ms threshold.");
    }
}
