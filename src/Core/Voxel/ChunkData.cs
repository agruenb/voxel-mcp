namespace VoxelKing.Core.Voxel;

using System;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// Flat array-backed storage for a 32x32x32 chunk of voxels.
/// </summary>
public sealed class ChunkData
{
    public const int Size = VoxelCoordinates.ChunkSize; // 32
    public const int SliceSize = Size * Size; // 1024
    public const int Volume = Size * Size * Size; // 32768

    private readonly ushort[] _blocks;

    public Vec3i ChunkCoord { get; }
    public bool IsDirty { get; set; }
    public uint Version { get; private set; }

    public ReadOnlySpan<ushort> Blocks => _blocks;

    public ChunkData(Vec3i chunkCoord)
    {
        ChunkCoord = chunkCoord;
        _blocks = new ushort[Volume];
        IsDirty = true;
        Version = 1;
    }

    public ChunkData(Vec3i chunkCoord, ushort[] blocks, uint version = 1, bool isDirty = false)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Length != Volume)
        {
            throw new ArgumentException($"Chunk block array must contain exactly {Volume} elements, but got {blocks.Length}.", nameof(blocks));
        }

        ChunkCoord = chunkCoord;
        _blocks = (ushort[])blocks.Clone();
        Version = version;
        IsDirty = isDirty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z)
    {
        return x + (z * Size) + (y * SliceSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3i GetLocalCoords(int index)
    {
        int x = index % Size;
        int y = index / SliceSize;
        int z = (index / Size) % Size;
        return new Vec3i(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlock(int x, int y, int z)
    {
        if ((uint)x >= Size || (uint)y >= Size || (uint)z >= Size)
        {
            throw new ArgumentOutOfRangeException($"Local coordinate ({x}, {y}, {z}) is out of bounds for chunk of size {Size}.");
        }
        return _blocks[GetIndex(x, y, z)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlock(Vec3i localPos) => GetBlock(localPos.X, localPos.Y, localPos.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, ushort blockId)
    {
        if ((uint)x >= Size || (uint)y >= Size || (uint)z >= Size)
        {
            throw new ArgumentOutOfRangeException($"Local coordinate ({x}, {y}, {z}) is out of bounds for chunk of size {Size}.");
        }

        int index = GetIndex(x, y, z);
        if (_blocks[index] != blockId)
        {
            _blocks[index] = blockId;
            IsDirty = true;
            Version++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(Vec3i localPos, ushort blockId) => SetBlock(localPos.X, localPos.Y, localPos.Z, blockId);

    /// <summary>
    /// Returns a deep copy of the underlying block array.
    /// </summary>
    public ushort[] GetRawBlocksCopy() => (ushort[])_blocks.Clone();

    /// <summary>
    /// Creates a complete snapshot of this chunk.
    /// </summary>
    public ChunkData Clone() => new(ChunkCoord, _blocks, Version, IsDirty);
}
