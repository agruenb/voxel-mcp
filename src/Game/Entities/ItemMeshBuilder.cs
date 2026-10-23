namespace VoxelKing.Game.Entities;

using System;
using Godot;
using VoxelKing.Core.Voxel;

/// <summary>
/// Constructs miniature block meshes for dropped item entities.
/// Vertices are constructed such that the bottom face of the cube rests exactly at local Y = 0,
/// ensuring dropped items lay directly flat on the ground without hovering.
/// </summary>
public static class ItemMeshBuilder
{
    public const float BlockItemSize = 0.28f;
    private const float HalfSize = BlockItemSize * 0.5f;

    /// <summary>
    /// Builds a miniature 3D voxel cube mesh for the given block definition with texture atlas UV mapping.
    /// </summary>
    public static ArrayMesh BuildBlockItemMesh(BlockDefinition? blockDef, TextureAtlasConfig? atlasConfig = null)
    {
        atlasConfig ??= TextureAtlasConfig.Default;

        var vertices = new Vector3[24];
        var normals = new Vector3[24];
        var uvs = new Vector2[24];
        var indices = new int[36];

        int vIndex = 0;
        int iIndex = 0;

        // 1. Top Face (+Y)
        int topTile = blockDef?.AtlasIndices.Top ?? 0;
        var (topMin, topMax) = atlasConfig.GetTileUV(topTile);
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            new Vector3(-HalfSize, BlockItemSize, -HalfSize),
            new Vector3(-HalfSize, BlockItemSize,  HalfSize),
            new Vector3( HalfSize, BlockItemSize,  HalfSize),
            new Vector3( HalfSize, BlockItemSize, -HalfSize),
            Vector3.Up,
            topMin, topMax
        );

        // 2. Bottom Face (-Y)
        int bottomTile = blockDef?.AtlasIndices.Bottom ?? 0;
        var (bottomMin, bottomMax) = atlasConfig.GetTileUV(bottomTile);
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            new Vector3(-HalfSize, 0f, -HalfSize),
            new Vector3( HalfSize, 0f, -HalfSize),
            new Vector3( HalfSize, 0f,  HalfSize),
            new Vector3(-HalfSize, 0f,  HalfSize),
            Vector3.Down,
            bottomMin, bottomMax
        );

        // 3. North Face (-Z)
        int northTile = blockDef?.AtlasIndices.North ?? 0;
        var (northMin, northMax) = atlasConfig.GetTileUV(northTile);
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            new Vector3( HalfSize, 0f,            -HalfSize),
            new Vector3(-HalfSize, 0f,            -HalfSize),
            new Vector3(-HalfSize, BlockItemSize, -HalfSize),
            new Vector3( HalfSize, BlockItemSize, -HalfSize),
            Vector3.Forward, // (0, 0, -1) in Godot
            northMin, northMax
        );

        // 4. South Face (+Z)
        int southTile = blockDef?.AtlasIndices.South ?? 0;
        var (southMin, southMax) = atlasConfig.GetTileUV(southTile);
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            new Vector3(-HalfSize, 0f,            HalfSize),
            new Vector3( HalfSize, 0f,            HalfSize),
            new Vector3( HalfSize, BlockItemSize, HalfSize),
            new Vector3(-HalfSize, BlockItemSize, HalfSize),
            Vector3.Back, // (0, 0, 1) in Godot
            southMin, southMax
        );

        // 5. West Face (-X)
        int westTile = blockDef?.AtlasIndices.West ?? 0;
        var (westMin, westMax) = atlasConfig.GetTileUV(westTile);
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            new Vector3(-HalfSize, 0f,            -HalfSize),
            new Vector3(-HalfSize, 0f,             HalfSize),
            new Vector3(-HalfSize, BlockItemSize,  HalfSize),
            new Vector3(-HalfSize, BlockItemSize, -HalfSize),
            Vector3.Left, // (-1, 0, 0) in Godot
            westMin, westMax
        );

        // 6. East Face (+X)
        int eastTile = blockDef?.AtlasIndices.East ?? 0;
        var (eastMin, eastMax) = atlasConfig.GetTileUV(eastTile);
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            new Vector3(HalfSize, 0f,             HalfSize),
            new Vector3(HalfSize, 0f,            -HalfSize),
            new Vector3(HalfSize, BlockItemSize, -HalfSize),
            new Vector3(HalfSize, BlockItemSize,  HalfSize),
            Vector3.Right, // (1, 0, 0) in Godot
            eastMin, eastMax
        );

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void AddFace(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        int[] indices,
        ref int vIndex,
        ref int iIndex,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        Vector3 normal,
        Core.Common.Vec2 uvMin,
        Core.Common.Vec2 uvMax)
    {
        int baseIndex = vIndex;

        vertices[vIndex] = p0;
        normals[vIndex] = normal;
        uvs[vIndex] = new Vector2(uvMin.X, uvMax.Y);
        vIndex++;

        vertices[vIndex] = p1;
        normals[vIndex] = normal;
        uvs[vIndex] = new Vector2(uvMax.X, uvMax.Y);
        vIndex++;

        vertices[vIndex] = p2;
        normals[vIndex] = normal;
        uvs[vIndex] = new Vector2(uvMax.X, uvMin.Y);
        vIndex++;

        vertices[vIndex] = p3;
        normals[vIndex] = normal;
        uvs[vIndex] = new Vector2(uvMin.X, uvMin.Y);
        vIndex++;

        indices[iIndex++] = baseIndex;
        indices[iIndex++] = baseIndex + 1;
        indices[iIndex++] = baseIndex + 2;

        indices[iIndex++] = baseIndex;
        indices[iIndex++] = baseIndex + 2;
        indices[iIndex++] = baseIndex + 3;
    }
}
