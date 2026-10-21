namespace VoxelKing.Core.Voxel;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Mesh buffer containing vertex positions, normals, UV texture coordinates, and triangle indices.
/// Completely decoupled from engine types.
/// </summary>
public sealed class MeshData
{
    public static readonly MeshData Empty = new(
        Array.Empty<Vec3>(),
        Array.Empty<Vec3>(),
        Array.Empty<Vec2>(),
        Array.Empty<int>()
    );

    public Vec3[] Vertices { get; }
    public Vec3[] Normals { get; }
    public Vec2[] UVs { get; }
    public int[] Indices { get; }

    public int VertexCount => Vertices.Length;
    public int IndexCount => Indices.Length;
    public int QuadCount => Indices.Length / 6;
    public int FaceCount => QuadCount;
    public int TriangleCount => Indices.Length / 3;
    public bool IsEmpty => Indices.Length == 0;

    public MeshData(Vec3[] vertices, Vec3[] normals, Vec2[] uvs, int[] indices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Normals = normals ?? throw new ArgumentNullException(nameof(normals));
        UVs = uvs ?? throw new ArgumentNullException(nameof(uvs));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));

        if (vertices.Length != normals.Length || vertices.Length != uvs.Length)
        {
            throw new ArgumentException("Vertices, Normals, and UV arrays must have equal lengths.");
        }
    }

    /// <summary>
    /// Returns the flattened array of triangle vertex positions (length = Indices.Length).
    /// Used for physics collision meshes and geometric queries.
    /// </summary>
    public Vec3[] GetTriangleVertices()
    {
        if (Indices.Length == 0) return Array.Empty<Vec3>();
        var result = new Vec3[Indices.Length];
        for (int i = 0; i < Indices.Length; i++)
        {
            result[i] = Vertices[Indices[i]];
        }
        return result;
    }
}
