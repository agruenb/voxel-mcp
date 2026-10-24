namespace VoxelKing.Core.Voxel;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Constructs miniature block mesh data for dropped item entities.
/// Vertices are constructed such that the bottom face of the cube rests exactly at local Y = 0,
/// ensuring dropped items lay directly flat on the ground without hovering.
/// Front-face triangle winding is clockwise (viewed from outside) to match Godot 4 cull_back conventions.
/// </summary>
public static class BlockItemMeshBuilder
{
    public const float BlockItemSize = 0.28f;
    private const float HalfSize = BlockItemSize * 0.5f;

    /// <summary>
    /// Builds a miniature 3D voxel cube MeshData for the given block definition with texture atlas UV mapping.
    /// </summary>
    public static MeshData BuildBlockItemMesh(BlockDefinition? blockDef, TextureAtlasConfig? atlasConfig = null)
    {
        atlasConfig ??= TextureAtlasConfig.Default;

        var vertices = new Vec3[24];
        var normals = new Vec3[24];
        var uvs = new Vec2[24];
        var indices = new int[36];

        int vIndex = 0;
        int iIndex = 0;

        float x0 = -HalfSize;
        float x1 = HalfSize;
        float y0 = 0f;
        float y1 = BlockItemSize;
        float z0 = -HalfSize;
        float z1 = HalfSize;

        // 1. Top Face (+Y)
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            BlockFace.Top,
            new Vec3(x0, y1, z0),
            new Vec3(x0, y1, z1),
            new Vec3(x1, y1, z1),
            new Vec3(x1, y1, z0),
            Vec3.Up,
            blockDef, atlasConfig
        );

        // 2. Bottom Face (-Y)
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            BlockFace.Bottom,
            new Vec3(x0, y0, z0),
            new Vec3(x1, y0, z0),
            new Vec3(x1, y0, z1),
            new Vec3(x0, y0, z1),
            Vec3.Down,
            blockDef, atlasConfig
        );

        // 3. East Face (+X)
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            BlockFace.East,
            new Vec3(x1, y0, z0),
            new Vec3(x1, y1, z0),
            new Vec3(x1, y1, z1),
            new Vec3(x1, y0, z1),
            Vec3.Right,
            blockDef, atlasConfig
        );

        // 4. West Face (-X)
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            BlockFace.West,
            new Vec3(x0, y0, z0),
            new Vec3(x0, y0, z1),
            new Vec3(x0, y1, z1),
            new Vec3(x0, y1, z0),
            Vec3.Left,
            blockDef, atlasConfig
        );

        // 5. South Face (+Z)
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            BlockFace.South,
            new Vec3(x0, y0, z1),
            new Vec3(x1, y0, z1),
            new Vec3(x1, y1, z1),
            new Vec3(x0, y1, z1),
            Vec3.Back,
            blockDef, atlasConfig
        );

        // 6. North Face (-Z)
        AddFace(
            vertices, normals, uvs, indices, ref vIndex, ref iIndex,
            BlockFace.North,
            new Vec3(x0, y0, z0),
            new Vec3(x0, y1, z0),
            new Vec3(x1, y1, z0),
            new Vec3(x1, y0, z0),
            Vec3.Forward,
            blockDef, atlasConfig
        );

        return new MeshData(vertices, normals, uvs, indices);
    }

    private static void AddFace(
        Vec3[] vertices,
        Vec3[] normals,
        Vec2[] uvs,
        int[] indices,
        ref int vIndex,
        ref int iIndex,
        BlockFace face,
        Vec3 v0,
        Vec3 v1,
        Vec3 v2,
        Vec3 v3,
        Vec3 normal,
        BlockDefinition? blockDef,
        TextureAtlasConfig atlasConfig)
    {
        int baseIndex = vIndex;

        int tileIndex = blockDef?.AtlasIndices.GetFaceIndex(face) ?? 0;
        var (uv0, uv1, uv2, uv3) = atlasConfig.GetFaceUVs(tileIndex, face);

        vertices[vIndex] = v0;
        normals[vIndex] = normal;
        uvs[vIndex] = uv0;
        vIndex++;

        vertices[vIndex] = v1;
        normals[vIndex] = normal;
        uvs[vIndex] = uv1;
        vIndex++;

        vertices[vIndex] = v2;
        normals[vIndex] = normal;
        uvs[vIndex] = uv2;
        vIndex++;

        vertices[vIndex] = v3;
        normals[vIndex] = normal;
        uvs[vIndex] = uv3;
        vIndex++;

        // Quad corners v0..v3 are CCW viewed along +normal.
        // Godot 4 treats clockwise triangles as front faces, so emit (0, 2, 1) and (0, 3, 2).
        // See docs/RENDER_AUDIT.md and ChunkMesher.
        indices[iIndex++] = baseIndex + 0;
        indices[iIndex++] = baseIndex + 2;
        indices[iIndex++] = baseIndex + 1;

        indices[iIndex++] = baseIndex + 0;
        indices[iIndex++] = baseIndex + 3;
        indices[iIndex++] = baseIndex + 2;
    }
}
