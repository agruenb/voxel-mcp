namespace VoxelKing.Core.Voxel;

using System;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// An immutable, thread-safe snapshot of a chunk (32x32x32) plus a 1-voxel border in all 6 directions (34x34x34 total).
/// Decoupled from IVoxelWorld for safe execution on background worker threads.
/// </summary>
public sealed class ChunkMeshSnapshot
{
    public const int ChunkSize = VoxelCoordinates.ChunkSize; // 32
    public const int PaddedSize = ChunkSize + 2; // 34
    public const int PaddedSliceSize = PaddedSize * PaddedSize; // 1156
    public const int PaddedVolume = PaddedSize * PaddedSize * PaddedSize; // 39304

    private readonly ushort[] _blocks;

    public Vec3i ChunkCoord { get; }
    public uint Version { get; }

    public ChunkMeshSnapshot(Vec3i chunkCoord, uint version, ushort[] paddedBlocks)
    {
        ArgumentNullException.ThrowIfNull(paddedBlocks);
        if (paddedBlocks.Length != PaddedVolume)
        {
            throw new ArgumentException(
                $"Padded block array must have exactly {PaddedVolume} elements, but got {paddedBlocks.Length}.",
                nameof(paddedBlocks)
            );
        }

        ChunkCoord = chunkCoord;
        Version = version;
        _blocks = (ushort[])paddedBlocks.Clone();
    }

    /// <summary>
    /// Creates a snapshot from a single chunk with empty (air) borders.
    /// </summary>
    public ChunkMeshSnapshot(ChunkData centerChunk)
        : this(centerChunk, static _ => null)
    {
    }

    /// <summary>
    /// Creates a snapshot from a center chunk using a neighbor chunk lookup function.
    /// </summary>
    public ChunkMeshSnapshot(ChunkData centerChunk, Func<Vec3i, ChunkData?> neighborLookup)
    {
        ArgumentNullException.ThrowIfNull(centerChunk);
        ArgumentNullException.ThrowIfNull(neighborLookup);

        ChunkCoord = centerChunk.ChunkCoord;
        Version = centerChunk.Version;
        _blocks = new ushort[PaddedVolume];

        // 1. Copy center chunk blocks
        for (int y = 0; y < ChunkSize; y++)
        {
            int py = y + 1;
            for (int z = 0; z < ChunkSize; z++)
            {
                int pz = z + 1;
                int destBase = 1 + (pz * PaddedSize) + (py * PaddedSliceSize);
                for (int x = 0; x < ChunkSize; x++)
                {
                    _blocks[destBase + x] = centerChunk.GetBlock(x, y, z);
                }
            }
        }

        // 2. West neighbor (-X): local x = 31 mapped to px = 0
        var westNeighbor = neighborLookup(ChunkCoord + Vec3i.Left);
        if (westNeighbor != null)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                int py = y + 1;
                for (int z = 0; z < ChunkSize; z++)
                {
                    int pz = z + 1;
                    _blocks[0 + (pz * PaddedSize) + (py * PaddedSliceSize)] = westNeighbor.GetBlock(ChunkSize - 1, y, z);
                }
            }
        }

        // 3. East neighbor (+X): local x = 0 mapped to px = 33
        var eastNeighbor = neighborLookup(ChunkCoord + Vec3i.Right);
        if (eastNeighbor != null)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                int py = y + 1;
                for (int z = 0; z < ChunkSize; z++)
                {
                    int pz = z + 1;
                    _blocks[(PaddedSize - 1) + (pz * PaddedSize) + (py * PaddedSliceSize)] = eastNeighbor.GetBlock(0, y, z);
                }
            }
        }

        // 4. Bottom neighbor (-Y): local y = 31 mapped to py = 0
        var bottomNeighbor = neighborLookup(ChunkCoord + Vec3i.Down);
        if (bottomNeighbor != null)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                int pz = z + 1;
                for (int x = 0; x < ChunkSize; x++)
                {
                    int px = x + 1;
                    _blocks[px + (pz * PaddedSize) + 0] = bottomNeighbor.GetBlock(x, ChunkSize - 1, z);
                }
            }
        }

        // 5. Top neighbor (+Y): local y = 0 mapped to py = 33
        var topNeighbor = neighborLookup(ChunkCoord + Vec3i.Up);
        if (topNeighbor != null)
        {
            int py = PaddedSize - 1;
            for (int z = 0; z < ChunkSize; z++)
            {
                int pz = z + 1;
                for (int x = 0; x < ChunkSize; x++)
                {
                    int px = x + 1;
                    _blocks[px + (pz * PaddedSize) + (py * PaddedSliceSize)] = topNeighbor.GetBlock(x, 0, z);
                }
            }
        }

        // 6. North neighbor (-Z): local z = 31 mapped to pz = 0
        var northNeighbor = neighborLookup(ChunkCoord + Vec3i.North);
        if (northNeighbor != null)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                int py = y + 1;
                for (int x = 0; x < ChunkSize; x++)
                {
                    int px = x + 1;
                    _blocks[px + 0 + (py * PaddedSliceSize)] = northNeighbor.GetBlock(x, y, ChunkSize - 1);
                }
            }
        }

        // 7. South neighbor (+Z): local z = 0 mapped to pz = 33
        var southNeighbor = neighborLookup(ChunkCoord + Vec3i.South);
        if (southNeighbor != null)
        {
            int pz = PaddedSize - 1;
            for (int y = 0; y < ChunkSize; y++)
            {
                int py = y + 1;
                for (int x = 0; x < ChunkSize; x++)
                {
                    int px = x + 1;
                    _blocks[px + (pz * PaddedSize) + (py * PaddedSliceSize)] = southNeighbor.GetBlock(x, y, 0);
                }
            }
        }
    }

    public static ChunkMeshSnapshot Create(ChunkData centerChunk, IVoxelWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new ChunkMeshSnapshot(centerChunk, coord => world.GetChunk(coord));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlock(int x, int y, int z)
    {
        int px = x + 1;
        int py = y + 1;
        int pz = z + 1;

        if ((uint)px >= PaddedSize || (uint)py >= PaddedSize || (uint)pz >= PaddedSize)
        {
            return 0; // Out-of-bounds defaults to Air
        }

        return _blocks[px + (pz * PaddedSize) + (py * PaddedSliceSize)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetBlock(Vec3i localCoord) => GetBlock(localCoord.X, localCoord.Y, localCoord.Z);

    public ushort this[int x, int y, int z] => GetBlock(x, y, z);
    public ushort this[Vec3i localCoord] => GetBlock(localCoord.X, localCoord.Y, localCoord.Z);
}
