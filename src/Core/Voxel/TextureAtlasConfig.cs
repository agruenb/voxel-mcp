namespace VoxelKing.Core.Voxel;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Layout configuration and UV coordinate calculation for a uniform grid texture atlas.
/// </summary>
public sealed record TextureAtlasConfig
{
    public static readonly TextureAtlasConfig Default = new(4, 4);

    public int Columns { get; init; }
    public int Rows { get; init; }

    public float TileWidth => 1.0f / Columns;
    public float TileHeight => 1.0f / Rows;

    public TextureAtlasConfig(int columns = 4, int rows = 4)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be positive.");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be positive.");

        Columns = columns;
        Rows = rows;
    }

    /// <summary>
    /// Returns the normalized UV bounding rectangle [uMin, vMin, uMax, vMax] for the given tile index.
    /// </summary>
    public (Vec2 Min, Vec2 Max) GetTileUV(int tileIndex)
    {
        int col = tileIndex % Columns;
        int row = tileIndex / Columns;

        float u0 = (float)col * TileWidth;
        float u1 = u0 + TileWidth;
        float v0 = (float)row * TileHeight;
        float v1 = v0 + TileHeight;

        return (new Vec2(u0, v0), new Vec2(u1, v1));
    }

    /// <summary>
    /// Calculates the 4 UV coordinates for the vertices of a quad corresponding to the given face.
    /// </summary>
    public (Vec2 UV0, Vec2 UV1, Vec2 UV2, Vec2 UV3) GetFaceUVs(int tileIndex, BlockFace face)
    {
        var (min, max) = GetTileUV(tileIndex);
        float u0 = min.X;
        float u1 = max.X;
        float v0 = min.Y;
        float v1 = max.Y;

        return face switch
        {
            BlockFace.Top => (
                new Vec2(u0, v0),
                new Vec2(u0, v1),
                new Vec2(u1, v1),
                new Vec2(u1, v0)
            ),
            BlockFace.Bottom => (
                new Vec2(u0, v0),
                new Vec2(u1, v0),
                new Vec2(u1, v1),
                new Vec2(u0, v1)
            ),
            BlockFace.East => (
                new Vec2(u0, v1),
                new Vec2(u0, v0),
                new Vec2(u1, v0),
                new Vec2(u1, v1)
            ),
            BlockFace.West => (
                new Vec2(u0, v1),
                new Vec2(u1, v1),
                new Vec2(u1, v0),
                new Vec2(u0, v0)
            ),
            BlockFace.South => (
                new Vec2(u0, v1),
                new Vec2(u1, v1),
                new Vec2(u1, v0),
                new Vec2(u0, v0)
            ),
            BlockFace.North => (
                new Vec2(u0, v1),
                new Vec2(u0, v0),
                new Vec2(u1, v0),
                new Vec2(u1, v1)
            ),
            _ => (
                new Vec2(u0, v0),
                new Vec2(u0, v1),
                new Vec2(u1, v1),
                new Vec2(u1, v0)
            ),
        };
    }
}
