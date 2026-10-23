namespace VoxelKing.Core.Tests.Persistence;

using System;
using System.IO;
using VoxelKing.Core.Common;
using VoxelKing.Core.Persistence;
using VoxelKing.Core.Voxel;
using Xunit;

public sealed class RleChunkSerializerTests
{
    private readonly BlockRegistry _registry = BlockRegistry.CreateDefault();

    [Fact]
    public void Serialize_EmptyChunk_ProducesMinimalRleRuns()
    {
        var coord = new Vec3i(1, 2, 3);
        var chunk = new ChunkData(coord, _registry.IsIndexed);

        byte[] bytes = RleChunkSerializer.Serialize(chunk);

        // Header (21 bytes) + runCount (4 bytes) + 1 run (4 bytes) = 29 bytes
        Assert.Equal(29, bytes.Length);

        var loaded = RleChunkSerializer.Deserialize(bytes, _registry.IsIndexed);
        Assert.Equal(coord, loaded.ChunkCoord);
        Assert.True(loaded.IsModified);
        Assert.Equal(0, loaded.GetBlock(0, 0, 0));
        Assert.Equal(0, loaded.GetBlock(15, 15, 15));
        Assert.Equal(0, loaded.GetBlock(31, 31, 31));
    }

    [Fact]
    public void Serialize_UniformSolidChunk_ProducesSingleRunAndRoundtrips()
    {
        var coord = new Vec3i(-5, 0, 10);
        ushort stoneId = _registry.GetId("stone");
        var chunk = new ChunkData(coord, _registry.IsIndexed);

        for (int z = 0; z < ChunkData.Size; z++)
        {
            for (int y = 0; y < ChunkData.Size; y++)
            {
                for (int x = 0; x < ChunkData.Size; x++)
                {
                    chunk.SetBlock(x, y, z, stoneId);
                }
            }
        }

        byte[] bytes = RleChunkSerializer.Serialize(chunk);
        Assert.Equal(29, bytes.Length);

        var loaded = RleChunkSerializer.Deserialize(bytes, _registry.IsIndexed);
        Assert.Equal(coord, loaded.ChunkCoord);
        Assert.True(loaded.IsModified);
        Assert.Equal(stoneId, loaded.GetBlock(0, 0, 0));
        Assert.Equal(stoneId, loaded.GetBlock(10, 20, 30));
        Assert.Equal(stoneId, loaded.GetBlock(31, 31, 31));
        Assert.Equal(ChunkData.Volume, loaded.GetBlockCount(stoneId));
    }

    [Fact]
    public void Serialize_LayeredTerrainChunk_RoundtripsAccurately()
    {
        var coord = new Vec3i(0, 0, 0);
        var chunk = new ChunkData(coord, _registry.IsIndexed);
        ushort stoneId = _registry.GetId("stone");
        ushort dirtId = _registry.GetId("dirt");
        ushort grassId = _registry.GetId("grass");

        // Y: 0..15 stone, 16..20 dirt, 21 grass, 22..31 air
        for (int y = 0; y < ChunkData.Size; y++)
        {
            ushort blockId = y switch
            {
                <= 15 => stoneId,
                <= 20 => dirtId,
                21 => grassId,
                _ => 0
            };

            for (int z = 0; z < ChunkData.Size; z++)
            {
                for (int x = 0; x < ChunkData.Size; x++)
                {
                    chunk.SetBlock(x, y, z, blockId);
                }
            }
        }

        byte[] bytes = RleChunkSerializer.Serialize(chunk);
        var loaded = RleChunkSerializer.Deserialize(bytes, _registry.IsIndexed);

        Assert.Equal(coord, loaded.ChunkCoord);
        Assert.Equal(stoneId, loaded.GetBlock(5, 10, 5));
        Assert.Equal(dirtId, loaded.GetBlock(5, 18, 5));
        Assert.Equal(grassId, loaded.GetBlock(5, 21, 5));
        Assert.Equal(0, loaded.GetBlock(5, 25, 5));

        Assert.Equal(16 * ChunkData.SliceSize, loaded.GetBlockCount(stoneId));
        Assert.Equal(5 * ChunkData.SliceSize, loaded.GetBlockCount(dirtId));
        Assert.Equal(ChunkData.SliceSize, loaded.GetBlockCount(grassId));
    }

    [Fact]
    public void Serialize_StreamOverload_MatchesByteArray()
    {
        var coord = new Vec3i(2, -1, 4);
        var chunk = new ChunkData(coord, _registry.IsIndexed);
        chunk.SetBlock(0, 0, 0, _registry.GetId("planks"));
        chunk.SetBlock(10, 10, 10, _registry.GetId("cobblestone"));

        byte[] bytes = RleChunkSerializer.Serialize(chunk);

        using var ms = new MemoryStream();
        RleChunkSerializer.Serialize(chunk, ms);
        byte[] streamBytes = ms.ToArray();

        Assert.Equal(bytes, streamBytes);

        ms.Position = 0;
        var loaded = RleChunkSerializer.Deserialize(ms, _registry.IsIndexed);
        Assert.Equal(coord, loaded.ChunkCoord);
        Assert.Equal(_registry.GetId("planks"), loaded.GetBlock(0, 0, 0));
        Assert.Equal(_registry.GetId("cobblestone"), loaded.GetBlock(10, 10, 10));
    }

    [Fact]
    public void Deserialize_InvalidMagic_ThrowsInvalidDataException()
    {
        byte[] corrupted = new byte[32];
        corrupted[0] = 0xFF; // Bad magic

        Assert.Throws<InvalidDataException>(() => RleChunkSerializer.Deserialize(corrupted));
    }

    [Fact]
    public void Deserialize_TruncatedPayload_ThrowsInvalidDataException()
    {
        var chunk = new ChunkData(new Vec3i(0, 0, 0), _registry.IsIndexed);
        byte[] valid = RleChunkSerializer.Serialize(chunk);

        // Truncate valid array
        byte[] truncated = valid[..^2];

        Assert.Throws<InvalidDataException>(() => RleChunkSerializer.Deserialize(truncated));
    }
}
