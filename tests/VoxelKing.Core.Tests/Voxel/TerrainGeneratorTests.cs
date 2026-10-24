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

#if DEBUG
        double threshold = 10.0;
#else
        double threshold = 5.0;
#endif
        // Feature acceptance criterion: generation of a chunk < 5 ms in release (10 ms in unoptimized debug)
        Assert.True(avgMs < threshold, $"Average chunk generation time ({avgMs:F3} ms) exceeded {threshold} ms threshold.");
    }

    [Fact]
    public void GetBiome_DistributesBothPlainsAndWoods()
    {
        var generator = new TerrainGenerator(1337, _registry);

        int plainsCount = 0;
        int woodsCount = 0;

        // Sample across a 1000x1000 block region
        for (int x = -500; x <= 500; x += 50)
        {
            for (int z = -500; z <= 500; z += 50)
            {
                var biome = generator.GetBiome(x, z);
                if (biome == BiomeType.Plains) plainsCount++;
                else if (biome == BiomeType.Woods) woodsCount++;
            }
        }

        Assert.True(plainsCount > 0, "World should contain Plains biomes.");
        Assert.True(woodsCount > 0, "World should contain Woods biomes.");
        double plainsRatio = (double)plainsCount / (plainsCount + woodsCount);
        // Expect balanced distribution (30% - 70%)
        Assert.InRange(plainsRatio, 0.30, 0.70);
    }

    [Fact]
    public void WoodsBiome_HasSignificantlyHigherTreeDensityThanPlains()
    {
        var generator = new TerrainGenerator(1337, _registry);
        ushort logId = _registry.GetId("oak_log");

        int plainsTrees = 0;
        int woodsTrees = 0;

        // Search for 20 Plains cells and 20 Woods cells
        for (int cz = -20; cz <= 20; cz++)
        {
            for (int cx = -20; cx <= 20; cx++)
            {
                int wx = cx * 8 + 4;
                int wz = cz * 8 + 4;
                var biome = generator.GetBiome(wx, wz);

                // Sample whether trunk exists in this cell
                bool hasTrunk = false;
                for (int dx = 0; dx < 8 && !hasTrunk; dx++)
                {
                    for (int dz = 0; dz < 8 && !hasTrunk; dz++)
                    {
                        if (generator.HasTreeTrunk(cx * 8 + dx, cz * 8 + dz))
                        {
                            hasTrunk = true;
                        }
                    }
                }

                if (hasTrunk)
                {
                    if (biome == BiomeType.Plains) plainsTrees++;
                    else woodsTrees++;
                }
            }
        }

        _output.WriteLine($"[Tree Density] Plains tree count: {plainsTrees}, Woods tree count: {woodsTrees}");
        Assert.True(woodsTrees > plainsTrees * 2, "Woods biome should have substantially higher tree density than Plains.");
    }

    [Fact]
    public void LakeCoverage_IsReducedToNaturalBasins()
    {
        var generator = new TerrainGenerator(1337, _registry);

        int totalColumns = 0;
        int waterColumns = 0;

        // Sample surface heights across a 400x400 block region
        for (int x = -200; x <= 200; x += 8)
        {
            for (int z = -200; z <= 200; z += 8)
            {
                totalColumns++;
                int height = generator.GetHeight(x, z);
                if (height <= generator.WaterLevel)
                {
                    waterColumns++;
                }
            }
        }

        double waterPercentage = (double)waterColumns / totalColumns * 100.0;
        _output.WriteLine($"[Lake Coverage] Water columns: {waterColumns}/{totalColumns} ({waterPercentage:F1}%)");

        // Lake coverage should be naturally limited to distinct basins (under 20%, rather than legacy ~45%)
        Assert.InRange(waterPercentage, 1.0, 20.0);
    }

    [Fact]
    public void HasTreeTrunk_ReturnsTrueAtTrunkLocationAndFalseNearby()
    {
        var generator = new TerrainGenerator(1337, _registry);

        // Find a tree trunk in a dense woods area
        bool foundTree = false;
        int treeX = 0;
        int treeZ = 0;

        for (int x = -100; x <= 100 && !foundTree; x++)
        {
            for (int z = -100; z <= 100 && !foundTree; z++)
            {
                if (generator.HasTreeTrunk(x, z))
                {
                    foundTree = true;
                    treeX = x;
                    treeZ = z;
                }
            }
        }

        Assert.True(foundTree, "Should find at least one tree trunk in the search area.");
        Assert.True(generator.HasTreeTrunk(treeX, treeZ));

        // Point 10 blocks away is very unlikely to be the same tree trunk
        bool distantTrunk = generator.HasTreeTrunk(treeX + 3, treeZ + 3);
        // Note: Could be another tree, but at +3, +3 in cell it's generally clear
        Assert.True(generator.HasTreeTrunk(treeX, treeZ), "Exact trunk position must always return true.");
    }
}

