namespace VoxelKing.Core.Voxel;

/// <summary>
/// Texture atlas tile indices for each face of a voxel.
/// </summary>
public readonly record struct BlockAtlasIndices(
    int Top,
    int Bottom,
    int North,
    int South,
    int East,
    int West)
{
    public BlockAtlasIndices(int all)
        : this(all, all, all, all, all, all)
    {
    }

    public BlockAtlasIndices(int top, int bottom, int sides)
        : this(top, bottom, sides, sides, sides, sides)
    {
    }

    public int GetFaceIndex(BlockFace face) => face switch
    {
        BlockFace.Top => Top,
        BlockFace.Bottom => Bottom,
        BlockFace.North => North,
        BlockFace.South => South,
        BlockFace.East => East,
        BlockFace.West => West,
        _ => North,
    };
}
