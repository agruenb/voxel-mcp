namespace VoxelKing.Core.Tests.Voxel;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;
using Xunit.Abstractions;

public class BlockTypeIndexTests
{
    private readonly ITestOutputHelper _output;
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();

    public BlockTypeIndexTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ChunkBlockIndex_InitialState_AllAir()
    {
        var chunk = new ChunkData(Vec3i.Zero, _registry.IsIndexed);

        Assert.Equal(ChunkData.Volume, chunk.GetBlockCount(_registry.AirId));
        Assert.Equal(0, chunk.GetBlockCount(_registry.GetId("oak_log")));
        Assert.Equal(0, chunk.GetBlockCount(_registry.GetId("stone")));
        Assert.Empty(chunk.GetIndexedPositions(_registry.GetId("oak_log")));
    }

    [Fact]
    public void ChunkBlockIndex_Mutation_UpdatesCountsAndPositions()
    {
        var chunk = new ChunkData(Vec3i.Zero, _registry.IsIndexed);
        ushort logId = _registry.GetId("oak_log");
        ushort chestId = _registry.GetId("chest");
        ushort stoneId = _registry.GetId("stone");
        ushort airId = _registry.AirId;

        // 1. Place indexed block (oak_log) at (5, 6, 7)
        chunk.SetBlock(5, 6, 7, logId);
        Assert.Equal(ChunkData.Volume - 1, chunk.GetBlockCount(airId));
        Assert.Equal(1, chunk.GetBlockCount(logId));
        Assert.Contains(new Vec3i(5, 6, 7), chunk.GetIndexedPositions(logId));

        // 2. Place non-indexed block (stone) at (10, 10, 10)
        chunk.SetBlock(10, 10, 10, stoneId);
        Assert.Equal(ChunkData.Volume - 2, chunk.GetBlockCount(airId));
        Assert.Equal(1, chunk.GetBlockCount(stoneId));
        Assert.Empty(chunk.GetIndexedPositions(stoneId)); // Stone is not indexed

        // 3. Overwrite log with another indexed block (chest)
        chunk.SetBlock(5, 6, 7, chestId);
        Assert.Equal(0, chunk.GetBlockCount(logId));
        Assert.Empty(chunk.GetIndexedPositions(logId));
        Assert.Equal(1, chunk.GetBlockCount(chestId));
        Assert.Contains(new Vec3i(5, 6, 7), chunk.GetIndexedPositions(chestId));

        // 4. Overwrite chest with non-indexed stone
        chunk.SetBlock(5, 6, 7, stoneId);
        Assert.Equal(0, chunk.GetBlockCount(chestId));
        Assert.Empty(chunk.GetIndexedPositions(chestId));
        Assert.Equal(2, chunk.GetBlockCount(stoneId));

        // 5. Clear stone back to air
        chunk.SetBlock(5, 6, 7, airId);
        chunk.SetBlock(10, 10, 10, airId);
        Assert.Equal(ChunkData.Volume, chunk.GetBlockCount(airId));
        Assert.Equal(0, chunk.GetBlockCount(stoneId));
    }

    [Fact]
    public void ChunkBlockIndex_CloneAndRebuild_MaintainsIntegrity()
    {
        var chunk = new ChunkData(new Vec3i(1, 2, 3), _registry.IsIndexed);
        ushort logId = _registry.GetId("oak_log");
        chunk.SetBlock(1, 2, 3, logId);
        chunk.SetBlock(4, 5, 6, logId);

        var clone = chunk.Clone();

        Assert.Equal(2, clone.GetBlockCount(logId));
        Assert.Equal(2, clone.GetIndexedPositions(logId).Count);
        Assert.Contains(new Vec3i(1, 2, 3), clone.GetIndexedPositions(logId));
        Assert.Contains(new Vec3i(4, 5, 6), clone.GetIndexedPositions(logId));

        // Mutating clone does not mutate original
        clone.SetBlock(1, 2, 3, _registry.AirId);
        Assert.Equal(1, clone.GetBlockCount(logId));
        Assert.Equal(2, chunk.GetBlockCount(logId));
    }

    [Fact]
    public void TerrainGenerator_IndexesLogsAndCoalOres()
    {
        var gen = new TerrainGenerator(1337, _registry);
        var chunk = gen.GenerateChunk(new Vec3i(0, 2, 0)); // Surface chunk containing trees

        ushort logId = _registry.GetId("oak_log");
        ushort coalId = _registry.GetId("coal_ore");

        int logCount = chunk.GetBlockCount(logId);
        var logPositions = chunk.GetIndexedPositions(logId);
        Assert.Equal(logCount, logPositions.Count);

        foreach (var pos in logPositions)
        {
            Assert.Equal(logId, chunk.GetBlock(pos));
        }

        int coalCount = chunk.GetBlockCount(coalId);
        var coalPositions = chunk.GetIndexedPositions(coalId);
        Assert.Equal(coalCount, coalPositions.Count);

        foreach (var pos in coalPositions)
        {
            Assert.Equal(coalId, chunk.GetBlock(pos));
        }
    }

    [Fact]
    public void FindNearestAndFindAll_MatchesBruteForceScan()
    {
        var world = new VoxelWorld(_registry);
        var gen = new TerrainGenerator(42, _registry);

        // Generate a 3x3 column of chunks around origin
        for (int cz = -1; cz <= 1; cz++)
        {
            for (int cy = 1; cy <= 3; cy++)
            {
                for (int cx = -1; cx <= 1; cx++)
                {
                    var chunk = gen.GenerateChunk(new Vec3i(cx, cy, cz));
                    world.AddOrUpdateChunk(chunk);
                }
            }
        }

        ushort logId = _registry.GetId("oak_log");
        ushort coalId = _registry.GetId("coal_ore");
        ushort stoneId = _registry.GetId("stone");

        var testOrigins = new[]
        {
            new Vec3i(0, 64, 0),
            new Vec3i(15, 70, 15),
            new Vec3i(-10, 50, -10),
            new Vec3i(31, 64, 31),
            new Vec3i(0, 0, 0),
        };

        int[] testRadii = { 5, 12, 24, 35 };

        foreach (var origin in testOrigins)
        {
            foreach (int radius in testRadii)
            {
                // Test indexed log
                VerifyAgainstBruteForce(world, logId, origin, radius);

                // Test indexed coal
                VerifyAgainstBruteForce(world, coalId, origin, radius);

                // Test non-indexed stone (fallback branch)
                VerifyAgainstBruteForce(world, stoneId, origin, radius, max: 16);
            }
        }
    }

    [Fact]
    public void StringOverloads_And_EdgeCases()
    {
        var world = new VoxelWorld(_registry);
        world.SetBlock(new Vec3i(5, 5, 5), _registry.GetId("chest"));

        // Valid query
        var nearest = world.FindNearest("chest", new Vec3i(0, 0, 0), 20);
        Assert.Equal(new Vec3i(5, 5, 5), nearest);

        var all = world.FindAll("chest", new Vec3i(0, 0, 0), 20, 10);
        Assert.Single(all);
        Assert.Equal(new Vec3i(5, 5, 5), all[0]);

        // Unknown string ID returns null / empty
        Assert.Null(world.FindNearest("non_existent_block", new Vec3i(0, 0, 0), 20));
        Assert.Empty(world.FindAll("non_existent_block", new Vec3i(0, 0, 0), 20, 10));

        // Negative radius or max <= 0
        Assert.Null(world.FindNearest("chest", new Vec3i(0, 0, 0), -5));
        Assert.Empty(world.FindAll("chest", new Vec3i(0, 0, 0), -5, 10));
        Assert.Empty(world.FindAll("chest", new Vec3i(0, 0, 0), 20, 0));
        Assert.Empty(world.FindAll("chest", new Vec3i(0, 0, 0), 20, -1));

        // Target outside radius
        Assert.Null(world.FindNearest("chest", new Vec3i(100, 100, 100), 10));
        Assert.Empty(world.FindAll("chest", new Vec3i(100, 100, 100), 10));
    }

    [Fact]
    public void PerformanceBenchmark_Radius24QueryUnder0Point2ms()
    {
        var world = new VoxelWorld(_registry);
        var gen = new TerrainGenerator(999, _registry);

        // Generate 3x3x3 = 27 chunks around origin
        for (int cz = -1; cz <= 1; cz++)
        {
            for (int cy = 1; cy <= 3; cy++)
            {
                for (int cx = -1; cx <= 1; cx++)
                {
                    var chunk = gen.GenerateChunk(new Vec3i(cx, cy, cz));
                    world.AddOrUpdateChunk(chunk);
                }
            }
        }

        ushort logId = _registry.GetId("oak_log");
        ushort coalId = _registry.GetId("coal_ore");
        var origin = new Vec3i(16, 68, 16);
        const int radius = 24;

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            world.FindNearest(logId, origin, radius);
            world.FindAll(coalId, origin, radius, 32);
        }

        // Benchmark 1000 FindNearest queries
        const int iterations = 1000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            world.FindNearest(logId, origin, radius);
        }
        sw.Stop();

        double avgMsNearest = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"[Benchmark] FindNearest(radius={radius}): {avgMsNearest * 1000:F2} µs ({avgMsNearest:F4} ms)");

        // Benchmark 1000 FindAll queries
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            world.FindAll(coalId, origin, radius, 32);
        }
        sw.Stop();

        double avgMsAll = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"[Benchmark] FindAll(radius={radius}, max=32): {avgMsAll * 1000:F2} µs ({avgMsAll:F4} ms)");

        // Acceptance criteria: query over radius 24 < 0.2 ms typical
        Assert.True(avgMsNearest < 0.2, $"FindNearest took {avgMsNearest:F4} ms, expected < 0.2 ms");
        Assert.True(avgMsAll < 0.2, $"FindAll took {avgMsAll:F4} ms, expected < 0.2 ms");
    }

    private void VerifyAgainstBruteForce(
        VoxelWorld world,
        ushort blockId,
        Vec3i origin,
        int radius,
        int max = 32)
    {
        // 1. Independent brute-force scan
        long radiusSq = (long)radius * radius;
        List<(Vec3i Pos, long DistSq)> bruteCandidates = new();

        foreach (var chunk in world.Chunks)
        {
            for (int ly = 0; ly < ChunkData.Size; ly++)
            {
                for (int lz = 0; lz < ChunkData.Size; lz++)
                {
                    for (int lx = 0; lx < ChunkData.Size; lx++)
                    {
                        if (chunk.GetBlock(lx, ly, lz) == blockId)
                        {
                            var worldPos = VoxelCoordinates.LocalToWorld(chunk.ChunkCoord, new Vec3i(lx, ly, lz));
                            long dx = (long)worldPos.X - origin.X;
                            long dy = (long)worldPos.Y - origin.Y;
                            long dz = (long)worldPos.Z - origin.Z;
                            long distSq = (dx * dx) + (dy * dy) + (dz * dz);

                            if (distSq <= radiusSq)
                            {
                                bruteCandidates.Add((worldPos, distSq));
                            }
                        }
                    }
                }
            }
        }

        bruteCandidates.Sort((a, b) =>
        {
            int c = a.DistSq.CompareTo(b.DistSq);
            if (c != 0) return c;
            c = a.Pos.X.CompareTo(b.Pos.X);
            if (c != 0) return c;
            c = a.Pos.Y.CompareTo(b.Pos.Y);
            if (c != 0) return c;
            return a.Pos.Z.CompareTo(b.Pos.Z);
        });

        Vec3i? expectedNearest = bruteCandidates.Count > 0 ? bruteCandidates[0].Pos : null;
        var expectedAll = bruteCandidates.Take(max).Select(c => c.Pos).ToArray();

        // 2. Query indexed world methods
        var actualNearest = world.FindNearest(blockId, origin, radius);
        var actualAll = world.FindAll(blockId, origin, radius, max);

        // 3. Assert exact equality
        Assert.Equal(expectedNearest, actualNearest);
        Assert.Equal(expectedAll.Length, actualAll.Count);
        for (int i = 0; i < expectedAll.Length; i++)
        {
            Assert.Equal(expectedAll[i], actualAll[i]);
        }
    }
}
