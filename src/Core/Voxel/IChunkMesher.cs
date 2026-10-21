namespace VoxelKing.Core.Voxel;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service contract for chunk meshing. Can be called on the main thread or offloaded to worker threads.
/// </summary>
public interface IChunkMesher
{
    TextureAtlasConfig AtlasConfig { get; }
    IBlockRegistry BlockRegistry { get; }

    /// <summary>
    /// Meshes a chunk snapshot synchronously using greedy meshing, face culling, and texture atlas UVs.
    /// </summary>
    ChunkMeshData Mesh(ChunkMeshSnapshot snapshot);

    /// <summary>
    /// Meshes a chunk snapshot asynchronously on a background worker thread.
    /// </summary>
    Task<ChunkMeshData> MeshAsync(ChunkMeshSnapshot snapshot, CancellationToken cancellationToken = default);
}
