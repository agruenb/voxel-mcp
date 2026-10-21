namespace VoxelKing.Core.Voxel;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;

/// <summary>
/// Greedy mesher implementing Lysenko's greedy meshing algorithm with neighbor face culling,
/// separate transparent submesh generation, and texture atlas UV mapping.
/// Safe to execute on background worker threads.
/// </summary>
public sealed class ChunkMesher : IChunkMesher
{
    private readonly struct FaceEntry : IEquatable<FaceEntry>
    {
        public readonly ushort BlockId;
        public readonly int TileIndex;
        public readonly bool IsTransparent;

        public bool HasFace => BlockId != 0;

        public FaceEntry(ushort blockId, int tileIndex, bool isTransparent)
        {
            BlockId = blockId;
            TileIndex = tileIndex;
            IsTransparent = isTransparent;
        }

        public bool Equals(FaceEntry other) =>
            BlockId == other.BlockId &&
            TileIndex == other.TileIndex &&
            IsTransparent == other.IsTransparent;

        public override bool Equals(object? obj) => obj is FaceEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BlockId, TileIndex, IsTransparent);
        public static bool operator ==(FaceEntry left, FaceEntry right) => left.Equals(right);
        public static bool operator !=(FaceEntry left, FaceEntry right) => !left.Equals(right);
    }

    private sealed class MeshBuilder
    {
        public List<Vec3> Vertices { get; } = new(512);
        public List<Vec3> Normals { get; } = new(512);
        public List<Vec2> UVs { get; } = new(512);
        public List<int> Indices { get; } = new(768);

        public void AddQuad(
            Vec3 v0, Vec3 v1, Vec3 v2, Vec3 v3,
            Vec3 normal,
            Vec2 uv0, Vec2 uv1, Vec2 uv2, Vec2 uv3)
        {
            int baseIndex = Vertices.Count;

            Vertices.Add(v0);
            Vertices.Add(v1);
            Vertices.Add(v2);
            Vertices.Add(v3);

            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);

            UVs.Add(uv0);
            UVs.Add(uv1);
            UVs.Add(uv2);
            UVs.Add(uv3);

            // CCW triangles: (0, 1, 2) and (0, 2, 3)
            Indices.Add(baseIndex + 0);
            Indices.Add(baseIndex + 1);
            Indices.Add(baseIndex + 2);
            Indices.Add(baseIndex + 0);
            Indices.Add(baseIndex + 2);
            Indices.Add(baseIndex + 3);
        }

        public MeshData ToMeshData()
        {
            if (Indices.Count == 0)
            {
                return MeshData.Empty;
            }

            return new MeshData(
                Vertices.ToArray(),
                Normals.ToArray(),
                UVs.ToArray(),
                Indices.ToArray()
            );
        }
    }

    public IBlockRegistry BlockRegistry { get; }
    public TextureAtlasConfig AtlasConfig { get; }

    public ChunkMesher(IBlockRegistry blockRegistry, TextureAtlasConfig? atlasConfig = null)
    {
        BlockRegistry = blockRegistry ?? throw new ArgumentNullException(nameof(blockRegistry));
        AtlasConfig = atlasConfig ?? TextureAtlasConfig.Default;
    }

    public ChunkMeshData Mesh(ChunkMeshSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var opaqueBuilder = new MeshBuilder();
        var transparentBuilder = new MeshBuilder();
        var mask = new FaceEntry[ChunkData.SliceSize];

        // 3 dimensions: 0 = X, 1 = Y, 2 = Z
        for (int d = 0; d < 3; d++)
        {
            int uAxis = (d + 1) % 3;
            int vAxis = (d + 2) % 3;

            // Slice boundaries s from 0 to 32
            for (int s = 0; s <= ChunkData.Size; s++)
            {
                // Direction 1: Positive normal (+d), block is inside chunk if s > 0
                if (s > 0)
                {
                    MeshSliceDirection(
                        snapshot,
                        d, uAxis, vAxis,
                        s,
                        isPositive: true,
                        mask,
                        opaqueBuilder,
                        transparentBuilder
                    );
                }

                // Direction 2: Negative normal (-d), block is inside chunk if s < ChunkData.Size
                if (s < ChunkData.Size)
                {
                    MeshSliceDirection(
                        snapshot,
                        d, uAxis, vAxis,
                        s,
                        isPositive: false,
                        mask,
                        opaqueBuilder,
                        transparentBuilder
                    );
                }
            }
        }

        return new ChunkMeshData(
            snapshot.ChunkCoord,
            snapshot.Version,
            opaqueBuilder.ToMeshData(),
            transparentBuilder.ToMeshData()
        );
    }

    public Task<ChunkMeshData> MeshAsync(ChunkMeshSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ChunkMeshData>(cancellationToken);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Mesh(snapshot);
        }, cancellationToken);
    }

    private void MeshSliceDirection(
        ChunkMeshSnapshot snapshot,
        int d, int uAxis, int vAxis,
        int s,
        bool isPositive,
        FaceEntry[] mask,
        MeshBuilder opaqueBuilder,
        MeshBuilder transparentBuilder)
    {
        BlockFace face = GetFace(d, isPositive);
        int blockCoordD = isPositive ? s - 1 : s;
        int neighborCoordD = isPositive ? s : s - 1;

        Array.Clear(mask, 0, mask.Length);
        bool anyFace = false;

        for (int v = 0; v < ChunkData.Size; v++)
        {
            for (int u = 0; u < ChunkData.Size; u++)
            {
                int bx = 0, by = 0, bz = 0;
                int nx = 0, ny = 0, nz = 0;

                SetCoord(ref bx, ref by, ref bz, d, blockCoordD, uAxis, u, vAxis, v);
                SetCoord(ref nx, ref ny, ref nz, d, neighborCoordD, uAxis, u, vAxis, v);

                ushort blockId = snapshot.GetBlock(bx, by, bz);
                if (blockId == 0) continue;

                ushort neighborId = snapshot.GetBlock(nx, ny, nz);
                var blockDef = BlockRegistry.GetDefinition(blockId);
                var neighborDef = BlockRegistry.GetDefinition(neighborId);

                if (ShouldRenderFace(blockDef, neighborDef))
                {
                    int tileIndex = blockDef.AtlasIndices.GetFaceIndex(face);
                    mask[u + (v * ChunkData.Size)] = new FaceEntry(blockId, tileIndex, blockDef.IsTransparent);
                    anyFace = true;
                }
            }
        }

        if (!anyFace) return;

        // 2D greedy merging on mask
        for (int v = 0; v < ChunkData.Size; v++)
        {
            for (int u = 0; u < ChunkData.Size; u++)
            {
                var entry = mask[u + (v * ChunkData.Size)];
                if (!entry.HasFace) continue;

                // Expand along u
                int w = 1;
                while (u + w < ChunkData.Size && mask[(u + w) + (v * ChunkData.Size)] == entry)
                {
                    w++;
                }

                // Expand along v
                int h = 1;
                bool canExtend = true;
                while (v + h < ChunkData.Size && canExtend)
                {
                    for (int k = 0; k < w; k++)
                    {
                        if (mask[(u + k) + ((v + h) * ChunkData.Size)] != entry)
                        {
                            canExtend = false;
                            break;
                        }
                    }

                    if (canExtend)
                    {
                        h++;
                    }
                }

                // Add merged quad
                var builder = entry.IsTransparent ? transparentBuilder : opaqueBuilder;
                AddQuad(builder, face, s, u, v, w, h, entry.TileIndex);

                // Clear merged region
                for (int dv = 0; dv < h; dv++)
                {
                    for (int du = 0; du < w; du++)
                    {
                        mask[(u + du) + ((v + dv) * ChunkData.Size)] = default;
                    }
                }

                u += w - 1;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetCoord(
        ref int x, ref int y, ref int z,
        int d, int valD,
        int uAxis, int valU,
        int vAxis, int valV)
    {
        if (d == 0) x = valD;
        else if (d == 1) y = valD;
        else z = valD;

        if (uAxis == 0) x = valU;
        else if (uAxis == 1) y = valU;
        else z = valU;

        if (vAxis == 0) x = valV;
        else if (vAxis == 1) y = valV;
        else z = valV;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockFace GetFace(int d, bool isPositive) => d switch
    {
        0 => isPositive ? BlockFace.East : BlockFace.West,
        1 => isPositive ? BlockFace.Top : BlockFace.Bottom,
        2 => isPositive ? BlockFace.South : BlockFace.North,
        _ => BlockFace.Top,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldRenderFace(BlockDefinition current, BlockDefinition neighbor)
    {
        if (current.Id == 0) return false;

        if (!current.IsTransparent)
        {
            // Opaque blocks render against any transparent block (including air)
            return neighbor.IsTransparent;
        }

        // Transparent blocks do not render against the same transparent block type
        if (neighbor.Id == current.Id)
        {
            return false;
        }

        // Transparent blocks do not render against solid opaque blocks
        if (!neighbor.IsTransparent)
        {
            return false;
        }

        return true;
    }

    private void AddQuad(
        MeshBuilder builder,
        BlockFace face,
        int s, int u, int v, int w, int h,
        int tileIndex)
    {
        var (uv0, uv1, uv2, uv3) = AtlasConfig.GetFaceUVs(tileIndex, face);

        Vec3 v0, v1, v2, v3;
        Vec3 normal;

        switch (face)
        {
            case BlockFace.Top:
                // d = 1 (Y), uAxis = 2 (Z), vAxis = 0 (X), normal = +Y
                // u is Z (width w), v is X (height h)
                normal = Vec3.Up;
                v0 = new Vec3(v, s, u);
                v1 = new Vec3(v, s, u + w);
                v2 = new Vec3(v + h, s, u + w);
                v3 = new Vec3(v + h, s, u);
                break;

            case BlockFace.Bottom:
                // d = 1 (Y), uAxis = 2 (Z), vAxis = 0 (X), normal = -Y
                normal = Vec3.Down;
                v0 = new Vec3(v, s, u);
                v1 = new Vec3(v + h, s, u);
                v2 = new Vec3(v + h, s, u + w);
                v3 = new Vec3(v, s, u + w);
                break;

            case BlockFace.East:
                // d = 0 (X), uAxis = 1 (Y), vAxis = 2 (Z), normal = +X
                // u is Y (width w), v is Z (height h)
                normal = Vec3.Right;
                v0 = new Vec3(s, u, v);
                v1 = new Vec3(s, u + w, v);
                v2 = new Vec3(s, u + w, v + h);
                v3 = new Vec3(s, u, v + h);
                break;

            case BlockFace.West:
                // d = 0 (X), uAxis = 1 (Y), vAxis = 2 (Z), normal = -X
                normal = Vec3.Left;
                v0 = new Vec3(s, u, v);
                v1 = new Vec3(s, u, v + h);
                v2 = new Vec3(s, u + w, v + h);
                v3 = new Vec3(s, u + w, v);
                break;

            case BlockFace.South:
                // d = 2 (Z), uAxis = 0 (X), vAxis = 1 (Y), normal = +Z
                // u is X (width w), v is Y (height h)
                normal = Vec3.Back;
                v0 = new Vec3(u, v, s);
                v1 = new Vec3(u + w, v, s);
                v2 = new Vec3(u + w, v + h, s);
                v3 = new Vec3(u, v + h, s);
                break;

            case BlockFace.North:
                // d = 2 (Z), uAxis = 0 (X), vAxis = 1 (Y), normal = -Z
                normal = Vec3.Forward;
                v0 = new Vec3(u, v, s);
                v1 = new Vec3(u, v + h, s);
                v2 = new Vec3(u + w, v + h, s);
                v3 = new Vec3(u + w, v, s);
                break;

            default:
                return;
        }

        builder.AddQuad(v0, v1, v2, v3, normal, uv0, uv1, uv2, uv3);
    }
}
