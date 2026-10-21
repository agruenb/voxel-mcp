namespace VoxelKing.Game.World;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Common;

/// <summary>
/// Godot MeshInstance3D node representing a single 32x32x32 voxel chunk in the scene tree.
/// Renders opaque and transparent submeshes using shared materials.
/// </summary>
public partial class ChunkNode : MeshInstance3D
{
    public Vec3i ChunkCoord { get; private set; }
    public uint MeshVersion { get; private set; }

    public ChunkNode()
        : this(Vec3i.Zero)
    {
    }

    public ChunkNode(Vec3i chunkCoord)
    {
        ChunkCoord = chunkCoord;
        Name = $"Chunk_{chunkCoord.X}_{chunkCoord.Y}_{chunkCoord.Z}";
        Position = new Vector3(
            chunkCoord.X * VoxelCoordinates.ChunkSize,
            chunkCoord.Y * VoxelCoordinates.ChunkSize,
            chunkCoord.Z * VoxelCoordinates.ChunkSize
        );
    }

    public void ApplyMesh(ChunkMeshData meshData, Material opaqueMaterial, Material transparentMaterial)
    {
        ArgumentNullException.ThrowIfNull(meshData);
        ArgumentNullException.ThrowIfNull(opaqueMaterial);
        ArgumentNullException.ThrowIfNull(transparentMaterial);

        MeshVersion = meshData.Version;

        if (meshData.IsEmpty)
        {
            Mesh = null;
            return;
        }

        var arrayMesh = new ArrayMesh();

        // Surface 0: Opaque blocks
        if (!meshData.Opaque.IsEmpty)
        {
            var opaqueArrays = BuildSurfaceArrays(meshData.Opaque);
            int surfIdx = arrayMesh.GetSurfaceCount();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, opaqueArrays);
            arrayMesh.SurfaceSetMaterial(surfIdx, opaqueMaterial);
        }

        // Surface 1: Transparent blocks (water, foliage)
        if (!meshData.Transparent.IsEmpty)
        {
            var transparentArrays = BuildSurfaceArrays(meshData.Transparent);
            int surfIdx = arrayMesh.GetSurfaceCount();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, transparentArrays);
            arrayMesh.SurfaceSetMaterial(surfIdx, transparentMaterial);
        }

        Mesh = arrayMesh;
    }

    private static Godot.Collections.Array BuildSurfaceArrays(MeshData meshData)
    {
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

        return arrays;
    }
}
