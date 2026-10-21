namespace VoxelKing.Core.Tests.Voxel;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class ChunkDataTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var coord = new Vec3i(1, -2, 3);
        var chunk = new ChunkData(coord);

        Assert.Equal(coord, chunk.ChunkCoord);
        Assert.True(chunk.IsDirty);
        Assert.Equal(1u, chunk.Version);
        Assert.Equal(ChunkData.Volume, chunk.Blocks.Length);

        // Every block should be initialized to Air (0)
        for (int i = 0; i < ChunkData.Volume; i++)
        {
            Assert.Equal((ushort)0, chunk.Blocks[i]);
        }
    }

    [Fact]
    public void IndexMapping_BijectionForAllVoxelsInChunk()
    {
        for (int index = 0; index < ChunkData.Volume; index++)
        {
            var coords = ChunkData.GetLocalCoords(index);
            Assert.InRange(coords.X, 0, ChunkData.Size - 1);
            Assert.InRange(coords.Y, 0, ChunkData.Size - 1);
            Assert.InRange(coords.Z, 0, ChunkData.Size - 1);

            int mappedIndex = ChunkData.GetIndex(coords.X, coords.Y, coords.Z);
            Assert.Equal(index, mappedIndex);
        }
    }

    [Fact]
    public void SetBlockAndGetBlock_ModifiesVoxelAndIncrementsVersion()
    {
        var chunk = new ChunkData(Vec3i.Zero) { IsDirty = false };
        uint initialVersion = chunk.Version;

        chunk.SetBlock(5, 10, 15, 3); // Stone

        Assert.Equal((ushort)3, chunk.GetBlock(5, 10, 15));
        Assert.Equal((ushort)3, chunk.GetBlock(new Vec3i(5, 10, 15)));
        Assert.True(chunk.IsDirty);
        Assert.Equal(initialVersion + 1, chunk.Version);

        // Setting the exact same block ID is a no-op: does not increment version
        chunk.IsDirty = false;
        chunk.SetBlock(5, 10, 15, 3);
        Assert.Equal(initialVersion + 1, chunk.Version);
        Assert.False(chunk.IsDirty);
    }

    [Fact]
    public void BoundsChecking_ThrowsOnOutOfBoundsAccess()
    {
        var chunk = new ChunkData(Vec3i.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetBlock(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetBlock(0, 32, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetBlock(0, 0, 32));

        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.SetBlock(-1, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.SetBlock(0, 32, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.SetBlock(0, 0, 32, 1));
    }

    [Fact]
    public void CloneAndRawCopy_CreatesDeepCopies()
    {
        var chunk = new ChunkData(new Vec3i(2, 3, 4));
        chunk.SetBlock(1, 2, 3, 10);

        var raw = chunk.GetRawBlocksCopy();
        Assert.Equal((ushort)10, raw[ChunkData.GetIndex(1, 2, 3)]);
        raw[ChunkData.GetIndex(1, 2, 3)] = 99;
        Assert.Equal((ushort)10, chunk.GetBlock(1, 2, 3)); // Original unchanged

        var clone = chunk.Clone();
        Assert.Equal(chunk.ChunkCoord, clone.ChunkCoord);
        Assert.Equal(chunk.Version, clone.Version);
        Assert.Equal(chunk.IsDirty, clone.IsDirty);
        Assert.Equal((ushort)10, clone.GetBlock(1, 2, 3));

        // Mutating clone does not mutate original
        clone.SetBlock(1, 2, 3, 5);
        Assert.Equal((ushort)5, clone.GetBlock(1, 2, 3));
        Assert.Equal((ushort)10, chunk.GetBlock(1, 2, 3));
    }

    [Fact]
    public void ArrayConstructor_ValidatesLength()
    {
        Assert.Throws<ArgumentException>(() => new ChunkData(Vec3i.Zero, new ushort[100]));
    }
}
