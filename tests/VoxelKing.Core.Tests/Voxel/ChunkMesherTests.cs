namespace VoxelKing.Core.Tests.Voxel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class ChunkMesherTests
{
    private readonly IBlockRegistry _registry;
    private readonly ChunkMesher _mesher;

    public ChunkMesherTests()
    {
        _registry = BlockRegistry.CreateDefault();
        _mesher = new ChunkMesher(_registry);
    }

    [Fact]
    public void EmptyChunk_ProducesEmptyMesh()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        Assert.True(mesh.IsEmpty);
        Assert.Equal(0, mesh.TotalFaceCount);
        Assert.Equal(0, mesh.TotalVertexCount);
        Assert.Equal(0, mesh.TotalTriangleCount);
        Assert.True(mesh.Opaque.IsEmpty);
        Assert.True(mesh.Transparent.IsEmpty);
    }

    [Fact]
    public void SingleVoxel_ProducesSixFaces()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");
        chunk.SetBlock(5, 5, 5, stoneId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        Assert.False(mesh.IsEmpty);
        Assert.Equal(6, mesh.Opaque.FaceCount);
        Assert.Equal(6, mesh.Opaque.QuadCount);
        Assert.Equal(12, mesh.Opaque.TriangleCount);
        Assert.Equal(24, mesh.Opaque.VertexCount);
        Assert.Equal(36, mesh.Opaque.Indices.Length);
        Assert.True(mesh.Transparent.IsEmpty);

        // Verify normal directions: exactly one quad per cardinal face
        var uniqueNormals = mesh.Opaque.Normals.Distinct().ToList();
        Assert.Equal(6, uniqueNormals.Count);
        Assert.Contains(Vec3.Up, uniqueNormals);
        Assert.Contains(Vec3.Down, uniqueNormals);
        Assert.Contains(Vec3.Right, uniqueNormals);
        Assert.Contains(Vec3.Left, uniqueNormals);
        Assert.Contains(Vec3.Forward, uniqueNormals);
        Assert.Contains(Vec3.Back, uniqueNormals);
    }

    [Fact]
    public void GreedyMeshing_2x2x2Cube_MergesIntoSixFaces()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        // 8 voxels in a 2x2x2 cube
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    chunk.SetBlock(x, y, z, stoneId);
                }
            }
        }

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        // Without greedy meshing, 6 sides * 4 quads = 24 quads.
        // With greedy meshing, each side merges into a single 2x2 quad -> 6 quads!
        Assert.Equal(6, mesh.Opaque.FaceCount);
        Assert.Equal(12, mesh.Opaque.TriangleCount);
        Assert.Equal(24, mesh.Opaque.VertexCount);
    }

    [Fact]
    public void GreedyMeshing_4x4FlatSlab_MergesIntoSixFaces()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        // 4x4 slab of height 1 (16 blocks)
        for (int x = 0; x < 4; x++)
        {
            for (int z = 0; z < 4; z++)
            {
                chunk.SetBlock(x, 0, z, stoneId);
            }
        }

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        // Top: 1 quad (4x4)
        // Bottom: 1 quad (4x4)
        // 4 sides: 4 quads (each 4x1)
        // Total: 6 quads!
        Assert.Equal(6, mesh.Opaque.FaceCount);
        Assert.Equal(12, mesh.Opaque.TriangleCount);
        Assert.Equal(24, mesh.Opaque.VertexCount);
    }

    [Fact]
    public void GreedyMeshing_3x1x1Row_MergesIntoSixFaces()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        chunk.SetBlock(1, 1, 1, stoneId);
        chunk.SetBlock(2, 1, 1, stoneId);
        chunk.SetBlock(3, 1, 1, stoneId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        Assert.Equal(6, mesh.Opaque.FaceCount);
    }

    [Fact]
    public void GreedyMeshing_LShape_MergesPartialRectangles()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        // L-shape: 3 blocks
        // (0,0,0), (1,0,0), (0,1,0)
        chunk.SetBlock(0, 0, 0, stoneId);
        chunk.SetBlock(1, 0, 0, stoneId);
        chunk.SetBlock(0, 1, 0, stoneId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        // Y: Top has 2 quads, Bottom has 1 quad (2x1) -> 3 quads
        // X: East has 2 quads, West has 1 quad (1x2) -> 3 quads
        // Z: North has 2 quads, South has 2 quads -> 4 quads
        // Total quads = 10
        Assert.Equal(10, mesh.Opaque.FaceCount);
    }

    [Fact]
    public void GreedyMeshing_DisconnectedCubes_ProduceSeparateFaces()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        // Two separated 1x1x1 cubes
        chunk.SetBlock(0, 0, 0, stoneId);
        chunk.SetBlock(10, 10, 10, stoneId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        Assert.Equal(12, mesh.Opaque.FaceCount);
    }

    [Fact]
    public void GreedyMeshing_DifferentBlockTypes_DoNotMerge()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");
        ushort dirtId = _registry.GetId("dirt");

        // Two adjacent blocks of different types
        chunk.SetBlock(0, 0, 0, stoneId);
        chunk.SetBlock(1, 0, 0, dirtId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        // Internal face at x=1 between stone and dirt is culled.
        // Top: 1 stone + 1 dirt = 2 quads (cannot merge different block types)
        // Bottom: 1 stone + 1 dirt = 2 quads
        // North: 1 stone + 1 dirt = 2 quads
        // South: 1 stone + 1 dirt = 2 quads
        // West: 1 stone = 1 quad
        // East: 1 dirt = 1 quad
        // Total: 10 quads
        Assert.Equal(10, mesh.Opaque.FaceCount);
    }

    [Fact]
    public void NeighborBorderCulling_CullsAcrossChunkBoundaries()
    {
        var world = new VoxelWorld(_registry);
        ushort stoneId = _registry.GetId("stone");

        var chunkA = world.GetOrCreateChunk(new Vec3i(0, 0, 0));
        var chunkB = world.GetOrCreateChunk(new Vec3i(1, 0, 0));

        // Place block at eastern border of Chunk A (x = 31)
        chunkA.SetBlock(31, 5, 5, stoneId);

        // Place block at western border of Chunk B (x = 0)
        chunkB.SetBlock(0, 5, 5, stoneId);

        // Case 1: Snapshot with neighbor world access
        var snapshotWithNeighbor = ChunkMeshSnapshot.Create(chunkA, world);
        var meshWithNeighbor = _mesher.Mesh(snapshotWithNeighbor);

        // East face at x=31 touches Chunk B's stone at x=0 -> culled!
        // Chunk A voxel has 5 visible faces (Top, Bottom, West, North, South)
        Assert.Equal(5, meshWithNeighbor.Opaque.FaceCount);
        Assert.DoesNotContain(Vec3.Right, meshWithNeighbor.Opaque.Normals);

        // Case 2: Standalone snapshot without neighbor (border is air)
        var snapshotIsolated = new ChunkMeshSnapshot(chunkA);
        var meshIsolated = _mesher.Mesh(snapshotIsolated);

        // East face faces air -> rendered! Total 6 faces.
        Assert.Equal(6, meshIsolated.Opaque.FaceCount);
        Assert.Contains(Vec3.Right, meshIsolated.Opaque.Normals);
    }

    [Fact]
    public void TransparentSubmesh_SeparatesOpaqueAndTransparentBlocks()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort waterId = _registry.GetId("water");
        ushort stoneId = _registry.GetId("stone");

        chunk.SetBlock(0, 0, 0, stoneId);
        chunk.SetBlock(0, 1, 0, waterId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        // Stone is opaque, Water is transparent.
        // Stone face towards water (at y=1) IS rendered in Opaque mesh.
        // Stone outer faces: Bottom, North, South, East, West (5) + Top (1) = 6 faces in Opaque.
        Assert.Equal(6, mesh.Opaque.FaceCount);

        // Water face towards stone (at y=1 looking down) is culled against solid opaque stone.
        // Water faces towards air (Top, North, South, East, West) = 5 faces in Transparent.
        Assert.Equal(5, mesh.Transparent.FaceCount);
    }

    [Fact]
    public void TransparentBlocks_AdjacentSameType_CullInternalFaces()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        ushort waterId = _registry.GetId("water");

        // Two adjacent water blocks
        chunk.SetBlock(0, 0, 0, waterId);
        chunk.SetBlock(1, 0, 0, waterId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = _mesher.Mesh(snapshot);

        Assert.Equal(0, mesh.Opaque.FaceCount);
        // Merged into 6 quads in transparent submesh!
        Assert.Equal(6, mesh.Transparent.FaceCount);
    }

    [Fact]
    public void TextureAtlasConfig_GeneratesNormalizedUVCoordinates()
    {
        var config = new TextureAtlasConfig(4, 4);
        var (min, max) = config.GetTileUV(0);

        Assert.Equal(0f, min.X);
        Assert.Equal(0f, min.Y);
        Assert.Equal(0.25f, max.X);
        Assert.Equal(0.25f, max.Y);

        var (min13, max13) = config.GetTileUV(13); // col 1, row 3
        Assert.Equal(0.25f, min13.X);
        Assert.Equal(0.75f, min13.Y);
        Assert.Equal(0.50f, max13.X);
        Assert.Equal(1.00f, max13.Y);

        var uvs = config.GetFaceUVs(0, BlockFace.Top);
        Assert.Equal(new Vec2(0f, 0f), uvs.UV0);
        Assert.Equal(new Vec2(0f, 0.25f), uvs.UV1);
        Assert.Equal(new Vec2(0.25f, 0.25f), uvs.UV2);
        Assert.Equal(new Vec2(0.25f, 0f), uvs.UV3);
    }

    [Fact]
    public async Task WorkerExecution_MeshesConcurrentlyAndThreadSafe()
    {
        ushort stoneId = _registry.GetId("stone");
        var tasks = new List<Task<ChunkMeshData>>();

        for (int i = 0; i < 8; i++)
        {
            var chunk = new ChunkData(new Vec3i(i, 0, 0));
            // Fill a 2x2x2 cube in each chunk
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        chunk.SetBlock(x, y, z, stoneId);
                    }
                }
            }

            var snapshot = new ChunkMeshSnapshot(chunk);
            tasks.Add(_mesher.MeshAsync(snapshot));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(8, results.Length);
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(new Vec3i(i, 0, 0), results[i].ChunkCoord);
            Assert.Equal(6, results[i].Opaque.FaceCount);
            Assert.True(results[i].Transparent.IsEmpty);
        }
    }

    [Fact]
    public async Task WorkerExecution_CancellationIsHonored()
    {
        var chunk = new ChunkData(Vec3i.Zero);
        var snapshot = new ChunkMeshSnapshot(chunk);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _mesher.MeshAsync(snapshot, cts.Token)
        );
    }

    [Fact]
    public void BlockEdit_RemeshesChunkAndUpdatesBorderNeighbor()
    {
        var bus = new EventBus();
        var world = new VoxelWorld(_registry, bus);
        ushort stoneId = _registry.GetId("stone");

        // Subscribe to BlockChanged to verify event bus propagation
        BlockChanged? receivedEvent = null;
        using var sub = bus.Subscribe<BlockChanged>(e => receivedEvent = e);

        // Pre-populate Chunk (1, 0, 0) with a stone block at local (0, 5, 5) -> world (32, 5, 5)
        world.SetBlock(new Vec3i(32, 5, 5), stoneId);

        var chunkB = world.GetChunk(new Vec3i(1, 0, 0))!;
        var snapshotBInitial = ChunkMeshSnapshot.Create(chunkB, world);
        var meshBInitial = _mesher.Mesh(snapshotBInitial);

        // West face of Chunk B's block currently looks into empty air in Chunk A -> 6 faces
        Assert.Equal(6, meshBInitial.Opaque.FaceCount);
        Assert.Contains(Vec3.Left, meshBInitial.Opaque.Normals);

        // Edit block in Chunk A at border (31, 5, 5)
        var editWorldPos = new Vec3i(31, 5, 5);
        world.SetBlock(editWorldPos, stoneId);

        // Verify EventBus received event immediately
        Assert.NotNull(receivedEvent);
        Assert.Equal(editWorldPos, receivedEvent.Value.Position);
        Assert.Equal((ushort)0, receivedEvent.Value.OldBlock);
        Assert.Equal(stoneId, receivedEvent.Value.NewBlock);

        // Verify both chunks are dirty
        var chunkA = world.GetChunk(new Vec3i(0, 0, 0))!;
        Assert.True(chunkA.IsDirty);
        Assert.True(chunkB.IsDirty);

        // Re-mesh Chunk A: its East face towards Chunk B at (32, 5, 5) is now culled! (5 faces, no Right normal)
        var snapshotA = ChunkMeshSnapshot.Create(chunkA, world);
        var meshA = _mesher.Mesh(snapshotA);
        Assert.Equal(5, meshA.Opaque.FaceCount);
        Assert.DoesNotContain(Vec3.Right, meshA.Opaque.Normals);

        // Re-mesh Chunk B: its West face towards Chunk A at (31, 5, 5) is now culled! (5 faces, no Left normal)
        var snapshotBUpdated = ChunkMeshSnapshot.Create(chunkB, world);
        var meshBUpdated = _mesher.Mesh(snapshotBUpdated);
        Assert.Equal(5, meshBUpdated.Opaque.FaceCount);
        Assert.DoesNotContain(Vec3.Left, meshBUpdated.Opaque.Normals);
    }
}
