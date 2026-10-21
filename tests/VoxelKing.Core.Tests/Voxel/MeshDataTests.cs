namespace VoxelKing.Core.Tests.Voxel;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class MeshDataTests
{
    [Fact]
    public void EmptyMeshData_GetTriangleVertices_ReturnsEmpty()
    {
        var mesh = MeshData.Empty;
        var triangles = mesh.GetTriangleVertices();

        Assert.Empty(triangles);
    }

    [Fact]
    public void SingleQuad_GetTriangleVertices_ReturnsSixVerticesInIndexOrder()
    {
        var vertices = new Vec3[]
        {
            new(0f, 0f, 0f),
            new(1f, 0f, 0f),
            new(1f, 1f, 0f),
            new(0f, 1f, 0f),
        };
        var normals = new Vec3[] { Vec3.Back, Vec3.Back, Vec3.Back, Vec3.Back };
        var uvs = new Vec2[] { Vec2.Zero, Vec2.UnitX, Vec2.One, Vec2.UnitY };
        var indices = new int[] { 0, 1, 2, 0, 2, 3 };

        var mesh = new MeshData(vertices, normals, uvs, indices);
        var triangleVertices = mesh.GetTriangleVertices();

        Assert.Equal(6, triangleVertices.Length);
        Assert.Equal(vertices[0], triangleVertices[0]);
        Assert.Equal(vertices[1], triangleVertices[1]);
        Assert.Equal(vertices[2], triangleVertices[2]);
        Assert.Equal(vertices[0], triangleVertices[3]);
        Assert.Equal(vertices[2], triangleVertices[4]);
        Assert.Equal(vertices[3], triangleVertices[5]);
    }

    [Fact]
    public void ChunkMesher_OpaqueMesh_GeneratesConsistentCollisionTriangles()
    {
        var registry = BlockRegistry.CreateDefault();
        var mesher = new ChunkMesher(registry);

        var chunk = new ChunkData(Vec3i.Zero);
        ushort stoneId = registry.GetId("stone");
        chunk.SetBlock(2, 3, 4, stoneId);

        var snapshot = new ChunkMeshSnapshot(chunk);
        var mesh = mesher.Mesh(snapshot);

        var collisionTriangles = mesh.Opaque.GetTriangleVertices();

        // 6 quads * 6 vertices per quad = 36 vertices
        Assert.Equal(36, collisionTriangles.Length);
        Assert.Equal(mesh.Opaque.Indices.Length, collisionTriangles.Length);

        // Every vertex must be within the block bounds [2..3, 3..4, 4..5]
        foreach (var v in collisionTriangles)
        {
            Assert.InRange(v.X, 2f, 3f);
            Assert.InRange(v.Y, 3f, 4f);
            Assert.InRange(v.Z, 4f, 5f);
        }
    }
}
