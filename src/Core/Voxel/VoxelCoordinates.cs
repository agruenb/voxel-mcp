namespace VoxelKing.Core.Voxel;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// Coordinate conversion arithmetic between world space, chunk space, and local block coordinates.
/// </summary>
public static class VoxelCoordinates
{
    public const int ChunkSize = 32;
    public const int ChunkShift = 5; // 1 << 5 == 32
    public const int ChunkMask = ChunkSize - 1; // 31

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldToChunk(int worldCoord) => worldCoord >> ChunkShift;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldToLocal(int worldCoord) => worldCoord & ChunkMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3i WorldToChunk(Vec3i worldPos) => new(
        WorldToChunk(worldPos.X),
        WorldToChunk(worldPos.Y),
        WorldToChunk(worldPos.Z)
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3i WorldToLocal(Vec3i worldPos) => new(
        WorldToLocal(worldPos.X),
        WorldToLocal(worldPos.Y),
        WorldToLocal(worldPos.Z)
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Vec3i Chunk, Vec3i Local) WorldToChunkAndLocal(Vec3i worldPos) => (
        WorldToChunk(worldPos),
        WorldToLocal(worldPos)
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3i LocalToWorld(Vec3i chunkCoord, Vec3i localCoord) => new(
        (chunkCoord.X << ChunkShift) + localCoord.X,
        (chunkCoord.Y << ChunkShift) + localCoord.Y,
        (chunkCoord.Z << ChunkShift) + localCoord.Z
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnBorder(Vec3i localCoord) =>
        localCoord.X == 0 || localCoord.X == ChunkMask ||
        localCoord.Y == 0 || localCoord.Y == ChunkMask ||
        localCoord.Z == 0 || localCoord.Z == ChunkMask;

    /// <summary>
    /// Gets neighbor chunk offset vectors for chunk borders touched by the given local coordinates.
    /// </summary>
    public static IEnumerable<Vec3i> GetBorderNeighborChunkOffsets(Vec3i localCoord)
    {
        if (localCoord.X == 0) yield return Vec3i.Left; // -X
        if (localCoord.X == ChunkMask) yield return Vec3i.Right; // +X
        if (localCoord.Y == 0) yield return Vec3i.Down; // -Y
        if (localCoord.Y == ChunkMask) yield return Vec3i.Up; // +Y
        if (localCoord.Z == 0) yield return Vec3i.North; // -Z
        if (localCoord.Z == ChunkMask) yield return Vec3i.South; // +Z
    }
}
