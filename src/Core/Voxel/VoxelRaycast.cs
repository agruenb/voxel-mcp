namespace VoxelKing.Core.Voxel;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Result of a digital differential analyzer (DDA) voxel raycast.
/// </summary>
public readonly record struct VoxelRaycastResult(
    bool IsHit,
    Vec3i BlockPos,
    Vec3i AdjacentPos,
    BlockFace Face,
    float Distance,
    ushort BlockId)
{
    public static readonly VoxelRaycastResult Miss = new(false, Vec3i.Zero, Vec3i.Zero, BlockFace.Top, 0f, 0);
}

/// <summary>
/// Fast 3D digital differential analyzer (DDA) grid raycasting for voxel worlds.
/// </summary>
public static class VoxelRaycast
{
    /// <summary>
    /// Casts a ray through the voxel grid using Amanatides-Woo DDA algorithm.
    /// </summary>
    public static VoxelRaycastResult Cast(
        Vec3 origin,
        Vec3 direction,
        float maxDistance,
        Func<Vec3i, ushort> getBlockId,
        Func<ushort, bool> isSolid)
    {
        ArgumentNullException.ThrowIfNull(getBlockId);
        ArgumentNullException.ThrowIfNull(isSolid);

        if (maxDistance <= 0f || direction.LengthSquared() < 1e-6f)
        {
            return VoxelRaycastResult.Miss;
        }

        Vec3 dir = direction.Normalized();

        int x = (int)MathF.Floor(origin.X);
        int y = (int)MathF.Floor(origin.Y);
        int z = (int)MathF.Floor(origin.Z);

        // Check if origin is already inside a solid block
        ushort startId = getBlockId(new Vec3i(x, y, z));
        if (isSolid(startId))
        {
            return new VoxelRaycastResult(true, new Vec3i(x, y, z), new Vec3i(x, y, z), BlockFace.Top, 0f, startId);
        }

        int stepX = dir.X > 0 ? 1 : (dir.X < 0 ? -1 : 0);
        int stepY = dir.Y > 0 ? 1 : (dir.Y < 0 ? -1 : 0);
        int stepZ = dir.Z > 0 ? 1 : (dir.Z < 0 ? -1 : 0);

        float tDeltaX = stepX != 0 ? MathF.Abs(1.0f / dir.X) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? MathF.Abs(1.0f / dir.Y) : float.PositiveInfinity;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1.0f / dir.Z) : float.PositiveInfinity;

        float tMaxX;
        if (stepX > 0) tMaxX = (x + 1.0f - origin.X) * tDeltaX;
        else if (stepX < 0) tMaxX = (origin.X - x) * tDeltaX;
        else tMaxX = float.PositiveInfinity;

        float tMaxY;
        if (stepY > 0) tMaxY = (y + 1.0f - origin.Y) * tDeltaY;
        else if (stepY < 0) tMaxY = (origin.Y - y) * tDeltaY;
        else tMaxY = float.PositiveInfinity;

        float tMaxZ;
        if (stepZ > 0) tMaxZ = (z + 1.0f - origin.Z) * tDeltaZ;
        else if (stepZ < 0) tMaxZ = (origin.Z - z) * tDeltaZ;
        else tMaxZ = float.PositiveInfinity;

        BlockFace face = BlockFace.Top;
        float currentDist = 0f;

        while (currentDist <= maxDistance)
        {
            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    currentDist = tMaxX;
                    tMaxX += tDeltaX;
                    x += stepX;
                    face = stepX > 0 ? BlockFace.West : BlockFace.East;
                }
                else
                {
                    currentDist = tMaxZ;
                    tMaxZ += tDeltaZ;
                    z += stepZ;
                    face = stepZ > 0 ? BlockFace.North : BlockFace.South;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    currentDist = tMaxY;
                    tMaxY += tDeltaY;
                    y += stepY;
                    face = stepY > 0 ? BlockFace.Bottom : BlockFace.Top;
                }
                else
                {
                    currentDist = tMaxZ;
                    tMaxZ += tDeltaZ;
                    z += stepZ;
                    face = stepZ > 0 ? BlockFace.North : BlockFace.South;
                }
            }

            if (currentDist > maxDistance)
            {
                break;
            }

            var hitPos = new Vec3i(x, y, z);
            ushort blockId = getBlockId(hitPos);

            if (isSolid(blockId))
            {
                var adjacentPos = hitPos + face.ToNormal();
                return new VoxelRaycastResult(true, hitPos, adjacentPos, face, currentDist, blockId);
            }
        }

        return VoxelRaycastResult.Miss;
    }

    /// <summary>
    /// Casts a ray through the voxel world searching for solid blocks.
    /// </summary>
    public static VoxelRaycastResult Cast(
        Vec3 origin,
        Vec3 direction,
        float maxDistance,
        IVoxelWorld world,
        IBlockRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(registry);

        return Cast(
            origin,
            direction,
            maxDistance,
            pos => world.GetBlock(pos),
            id => id != registry.AirId && registry.GetDefinition(id).IsSolid
        );
    }
}
