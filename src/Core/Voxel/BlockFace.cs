namespace VoxelKing.Core.Voxel;

using VoxelKing.Core.Common;

/// <summary>
/// Cardinal face directions of a 3D cubic voxel.
/// </summary>
public enum BlockFace
{
    Top,
    Bottom,
    North,
    South,
    East,
    West,
}

/// <summary>
/// Extension methods for voxel face calculations and normal vector mapping.
/// </summary>
public static class BlockFaceExtensions
{
    public static Vec3i ToNormal(this BlockFace face) => face switch
    {
        BlockFace.Top => Vec3i.Up,
        BlockFace.Bottom => Vec3i.Down,
        BlockFace.North => Vec3i.North,
        BlockFace.South => Vec3i.South,
        BlockFace.East => Vec3i.East,
        BlockFace.West => Vec3i.West,
        _ => Vec3i.Zero,
    };

    public static BlockFace Opposite(this BlockFace face) => face switch
    {
        BlockFace.Top => BlockFace.Bottom,
        BlockFace.Bottom => BlockFace.Top,
        BlockFace.North => BlockFace.South,
        BlockFace.South => BlockFace.North,
        BlockFace.East => BlockFace.West,
        BlockFace.West => BlockFace.East,
        _ => face,
    };
}
