namespace VoxelKing.Core.Voxel;

using VoxelKing.Core.Common;

/// <summary>
/// Generates deterministic voxel terrain chunks and provides surface height queries.
/// </summary>
public interface ITerrainGenerator
{
    /// <summary>
    /// The world generation seed.
    /// </summary>
    int Seed { get; }

    /// <summary>
    /// Sea/water level in world height coordinates.
    /// </summary>
    int WaterLevel { get; }

    /// <summary>
    /// Computes the surface terrain height at the given world coordinates (excluding trees).
    /// </summary>
    int GetHeight(int worldX, int worldZ);

    /// <summary>
    /// Generates a new populated ChunkData for the given chunk coordinates.
    /// </summary>
    ChunkData GenerateChunk(Vec3i chunkCoord);

    /// <summary>
    /// Populates an existing ChunkData instance with generated terrain.
    /// </summary>
    void PopulateChunk(ChunkData chunk);

    /// <summary>
    /// Checks if a procedural tree trunk exists at the specified world X and Z coordinates.
    /// </summary>
    bool HasTreeTrunk(int worldX, int worldZ);
}
