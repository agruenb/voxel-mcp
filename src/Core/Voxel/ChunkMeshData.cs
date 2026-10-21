namespace VoxelKing.Core.Voxel;

using VoxelKing.Core.Common;

/// <summary>
/// Combined mesh data for a chunk, containing separate submeshes for opaque and transparent blocks.
/// </summary>
public sealed class ChunkMeshData
{
    public Vec3i ChunkCoord { get; }
    public uint Version { get; }
    public MeshData Opaque { get; }
    public MeshData Transparent { get; }

    public bool IsEmpty => Opaque.IsEmpty && Transparent.IsEmpty;
    public int TotalFaceCount => Opaque.FaceCount + Transparent.FaceCount;
    public int TotalQuadCount => Opaque.QuadCount + Transparent.QuadCount;
    public int TotalTriangleCount => Opaque.TriangleCount + Transparent.TriangleCount;
    public int TotalVertexCount => Opaque.VertexCount + Transparent.VertexCount;

    public ChunkMeshData(Vec3i chunkCoord, uint version, MeshData opaque, MeshData transparent)
    {
        ChunkCoord = chunkCoord;
        Version = version;
        Opaque = opaque ?? MeshData.Empty;
        Transparent = transparent ?? MeshData.Empty;
    }

    public static ChunkMeshData CreateEmpty(Vec3i chunkCoord, uint version = 0) =>
        new(chunkCoord, version, MeshData.Empty, MeshData.Empty);
}
