namespace VoxelKing.Game.Entities;

using System;
using Godot;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Common;

/// <summary>
/// Constructs miniature block meshes for dropped item entities.
/// Vertices are constructed such that the bottom face of the cube rests exactly at local Y = 0,
/// ensuring dropped items lay directly flat on the ground without hovering.
/// Delegates core mesh geometry generation to <see cref="BlockItemMeshBuilder"/>.
/// </summary>
public static class ItemMeshBuilder
{
    public const float BlockItemSize = BlockItemMeshBuilder.BlockItemSize;

    /// <summary>
    /// Builds a miniature 3D voxel cube mesh for the given block definition with texture atlas UV mapping.
    /// </summary>
    public static ArrayMesh BuildBlockItemMesh(BlockDefinition? blockDef, TextureAtlasConfig? atlasConfig = null)
    {
        var meshData = BlockItemMeshBuilder.BuildBlockItemMesh(blockDef, atlasConfig);

        var vertices = new Vector3[meshData.Vertices.Length];
        var normals = new Vector3[meshData.Normals.Length];
        var uvs = new Vector2[meshData.UVs.Length];

        for (int i = 0; i < meshData.Vertices.Length; i++)
        {
            vertices[i] = meshData.Vertices[i].ToGodot();
            normals[i] = meshData.Normals[i].ToGodot();
            uvs[i] = meshData.UVs[i].ToGodot();
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = meshData.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}

