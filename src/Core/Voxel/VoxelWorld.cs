namespace VoxelKing.Core.Voxel;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VoxelKing.Core.Common;

/// <summary>
/// Central voxel world simulation owning chunk storage, block mutations, border invalidation, and change events.
/// </summary>
public sealed class VoxelWorld : IVoxelWorld
{
    private readonly ConcurrentDictionary<Vec3i, ChunkData> _chunks = new();
    private readonly IEventBus? _eventBus;

    public IBlockRegistry BlockRegistry { get; }
    public int ChunkCount => _chunks.Count;
    public IEnumerable<ChunkData> Chunks => _chunks.Values;

    public VoxelWorld(IBlockRegistry blockRegistry, IEventBus? eventBus = null)
    {
        BlockRegistry = blockRegistry ?? throw new ArgumentNullException(nameof(blockRegistry));
        _eventBus = eventBus;
    }

    public ushort GetBlock(Vec3i worldPos)
    {
        var (chunkCoord, localCoord) = VoxelCoordinates.WorldToChunkAndLocal(worldPos);
        if (_chunks.TryGetValue(chunkCoord, out var chunk))
        {
            return chunk.GetBlock(localCoord);
        }
        return BlockRegistry.AirId;
    }

    public ushort GetBlock(int worldX, int worldY, int worldZ) =>
        GetBlock(new Vec3i(worldX, worldY, worldZ));

    public bool SetBlock(Vec3i worldPos, ushort blockId)
    {
        var (chunkCoord, localCoord) = VoxelCoordinates.WorldToChunkAndLocal(worldPos);

        if (!_chunks.TryGetValue(chunkCoord, out var chunk))
        {
            if (blockId == BlockRegistry.AirId)
            {
                return false;
            }
            chunk = GetOrCreateChunk(chunkCoord);
        }

        ushort oldBlock = chunk.GetBlock(localCoord);
        if (oldBlock == blockId)
        {
            return false;
        }

        chunk.SetBlock(localCoord, blockId);
        chunk.IsDirty = true;

        // Border dirty logic: mark adjacent loaded neighbor chunks dirty when border voxels change
        foreach (var offset in VoxelCoordinates.GetBorderNeighborChunkOffsets(localCoord))
        {
            var neighborCoord = chunkCoord + offset;
            if (_chunks.TryGetValue(neighborCoord, out var neighborChunk))
            {
                neighborChunk.IsDirty = true;
            }
        }

        _eventBus?.Publish(new BlockChanged(worldPos, oldBlock, blockId));
        return true;
    }

    public bool SetBlock(int worldX, int worldY, int worldZ, ushort blockId) =>
        SetBlock(new Vec3i(worldX, worldY, worldZ), blockId);

    public bool HasChunk(Vec3i chunkCoord) => _chunks.ContainsKey(chunkCoord);

    public ChunkData? GetChunk(Vec3i chunkCoord) =>
        _chunks.TryGetValue(chunkCoord, out var chunk) ? chunk : null;

    public ChunkData GetOrCreateChunk(Vec3i chunkCoord) =>
        _chunks.GetOrAdd(chunkCoord, static coord => new ChunkData(coord));

    public void AddOrUpdateChunk(ChunkData chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _chunks[chunk.ChunkCoord] = chunk;
    }

    public bool RemoveChunk(Vec3i chunkCoord) =>
        _chunks.TryRemove(chunkCoord, out _);
}
