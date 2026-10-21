namespace VoxelKing.Core.Voxel;

using System.Collections.Generic;
using VoxelKing.Core.Common;

/// <summary>
/// World-scale voxel storage and query interface.
/// </summary>
public interface IVoxelWorld
{
    /// <summary>
    /// Gets the block definition registry associated with this world.
    /// </summary>
    IBlockRegistry BlockRegistry { get; }

    /// <summary>
    /// Total number of loaded chunks.
    /// </summary>
    int ChunkCount { get; }

    /// <summary>
    /// Gets all currently loaded chunks.
    /// </summary>
    IEnumerable<ChunkData> Chunks { get; }

    /// <summary>
    /// Gets the block ID at the specified world coordinates.
    /// Returns Air (0) if the chunk is not loaded.
    /// </summary>
    ushort GetBlock(Vec3i worldPos);

    /// <summary>
    /// Gets the block ID at the specified world coordinates.
    /// </summary>
    ushort GetBlock(int worldX, int worldY, int worldZ);

    /// <summary>
    /// Sets the block ID at the specified world coordinates.
    /// Marks the chunk and any border neighbors dirty, and publishes BlockChanged on the event bus.
    /// Returns true if the block was modified, or false if it was already the same block.
    /// </summary>
    bool SetBlock(Vec3i worldPos, ushort blockId);

    /// <summary>
    /// Sets the block ID at the specified world coordinates.
    /// </summary>
    bool SetBlock(int worldX, int worldY, int worldZ, ushort blockId);

    /// <summary>
    /// Determines whether a chunk is currently loaded at the given chunk coordinate.
    /// </summary>
    bool HasChunk(Vec3i chunkCoord);

    /// <summary>
    /// Gets the chunk at the specified chunk coordinate, or null if not loaded.
    /// </summary>
    ChunkData? GetChunk(Vec3i chunkCoord);

    /// <summary>
    /// Gets the chunk at the specified chunk coordinate, or creates and stores a new empty chunk if not present.
    /// </summary>
    ChunkData GetOrCreateChunk(Vec3i chunkCoord);

    /// <summary>
    /// Inserts or replaces a chunk in the world storage.
    /// </summary>
    void AddOrUpdateChunk(ChunkData chunk);

    /// <summary>
    /// Unloads and removes a chunk at the specified chunk coordinate.
    /// </summary>
    bool RemoveChunk(Vec3i chunkCoord);
}
