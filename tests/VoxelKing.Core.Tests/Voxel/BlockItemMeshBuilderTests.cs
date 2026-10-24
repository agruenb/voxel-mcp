namespace VoxelKing.Core.Tests.Voxel;

using System;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class BlockItemMeshBuilderTests
{
    private readonly IBlockRegistry _registry;

    public BlockItemMeshBuilderTests()
    {
        _registry = BlockRegistry.CreateDefault();
    }

    [Fact]
    public void AllTriangles_AreClockwiseFromOutside_ForGodotFrontFaces()
    {
        // Godot 4: front face = clockwise as seen by the camera. With a right-handed cross
        // product, a clockwise triangle seen from outside has (b-a)x(c-a) pointing INTO the block,
        // i.e. opposite to the outward vertex normal (dot < 0).
        var grassDef = _registry.GetDefinition(_registry.GetId("grass"));
        var mesh = BlockItemMeshBuilder.BuildBlockItemMesh(grassDef);

        Assert.Equal(12, mesh.Indices.Length / 3);
        for (int t = 0; t < mesh.Indices.Length; t += 3)
        {
            Vec3 a = mesh.Vertices[mesh.Indices[t]];
            Vec3 b = mesh.Vertices[mesh.Indices[t + 1]];
            Vec3 c = mesh.Vertices[mesh.Indices[t + 2]];

            Vec3 e1 = new(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            Vec3 e2 = new(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
            Vec3 cross = new(
                e1.Y * e2.Z - e1.Z * e2.Y,
                e1.Z * e2.X - e1.X * e2.Z,
                e1.X * e2.Y - e1.Y * e2.X
            );

            Vec3 normal = mesh.Normals[mesh.Indices[t]];
            float dot = cross.X * normal.X + cross.Y * normal.Y + cross.Z * normal.Z;

            Assert.True(dot < 0f, $"Triangle {t / 3} with normal {normal} is CCW from outside and will be culled by Godot.");
        }
    }

    [Fact]
    public void BlockItemMesh_HasSixFacesAndCorrectNormals()
    {
        var stoneDef = _registry.GetDefinition(_registry.GetId("stone"));
        var mesh = BlockItemMeshBuilder.BuildBlockItemMesh(stoneDef);

        Assert.Equal(24, mesh.Vertices.Length);
        Assert.Equal(24, mesh.Normals.Length);
        Assert.Equal(24, mesh.UVs.Length);
        Assert.Equal(36, mesh.Indices.Length);
        Assert.Equal(6, mesh.FaceCount);
        Assert.Equal(12, mesh.TriangleCount);

        var uniqueNormals = mesh.Normals.Distinct().ToList();
        Assert.Equal(6, uniqueNormals.Count);
        Assert.Contains(Vec3.Up, uniqueNormals);
        Assert.Contains(Vec3.Down, uniqueNormals);
        Assert.Contains(Vec3.Right, uniqueNormals);
        Assert.Contains(Vec3.Left, uniqueNormals);
        Assert.Contains(Vec3.Forward, uniqueNormals);
        Assert.Contains(Vec3.Back, uniqueNormals);
    }

    [Fact]
    public void BlockItemMesh_GeometryRestsOnGround_AndHasCorrectDimensions()
    {
        var dirtDef = _registry.GetDefinition(_registry.GetId("dirt"));
        var mesh = BlockItemMeshBuilder.BuildBlockItemMesh(dirtDef);

        float minY = mesh.Vertices.Min(v => v.Y);
        float maxY = mesh.Vertices.Max(v => v.Y);
        float minX = mesh.Vertices.Min(v => v.X);
        float maxX = mesh.Vertices.Max(v => v.X);
        float minZ = mesh.Vertices.Min(v => v.Z);
        float maxZ = mesh.Vertices.Max(v => v.Z);

        Assert.Equal(0f, minY, 4);
        Assert.Equal(BlockItemMeshBuilder.BlockItemSize, maxY, 4);

        float expectedHalf = BlockItemMeshBuilder.BlockItemSize * 0.5f;
        Assert.Equal(-expectedHalf, minX, 4);
        Assert.Equal(expectedHalf, maxX, 4);
        Assert.Equal(-expectedHalf, minZ, 4);
        Assert.Equal(expectedHalf, maxZ, 4);
    }

    [Fact]
    public void BlockItemMesh_TextureAtlasUVs_MatchPerFaceTiles()
    {
        // Grass has: Top = 0, Bottom = 2, Sides = 1
        var grassDef = _registry.GetDefinition(_registry.GetId("grass"));
        var atlasConfig = new TextureAtlasConfig(4, 4);
        var mesh = BlockItemMeshBuilder.BuildBlockItemMesh(grassDef, atlasConfig);

        // Find vertices by normal
        for (int i = 0; i < 24; i += 4)
        {
            Vec3 normal = mesh.Normals[i];
            BlockFace face;
            int expectedTile;

            if (normal == Vec3.Up)
            {
                face = BlockFace.Top;
                expectedTile = grassDef.AtlasIndices.Top;
            }
            else if (normal == Vec3.Down)
            {
                face = BlockFace.Bottom;
                expectedTile = grassDef.AtlasIndices.Bottom;
            }
            else if (normal == Vec3.Right)
            {
                face = BlockFace.East;
                expectedTile = grassDef.AtlasIndices.East;
            }
            else if (normal == Vec3.Left)
            {
                face = BlockFace.West;
                expectedTile = grassDef.AtlasIndices.West;
            }
            else if (normal == Vec3.Back)
            {
                face = BlockFace.South;
                expectedTile = grassDef.AtlasIndices.South;
            }
            else
            {
                face = BlockFace.North;
                expectedTile = grassDef.AtlasIndices.North;
            }

            var expectedUVs = atlasConfig.GetFaceUVs(expectedTile, face);
            Assert.Equal(expectedUVs.UV0, mesh.UVs[i]);
            Assert.Equal(expectedUVs.UV1, mesh.UVs[i + 1]);
            Assert.Equal(expectedUVs.UV2, mesh.UVs[i + 2]);
            Assert.Equal(expectedUVs.UV3, mesh.UVs[i + 3]);
        }
    }

    [Fact]
    public void BlockItemMesh_DefaultNullBlockDef_ProducesValidMesh()
    {
        var mesh = BlockItemMeshBuilder.BuildBlockItemMesh(null);

        Assert.Equal(24, mesh.Vertices.Length);
        Assert.Equal(36, mesh.Indices.Length);
        Assert.False(mesh.IsEmpty);
    }
}
