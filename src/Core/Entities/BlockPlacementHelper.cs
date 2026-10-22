namespace VoxelKing.Core.Entities;

using VoxelKing.Core.Common;

/// <summary>
/// Helper for validating block placement rules such as entity collision and boundary clearance.
/// </summary>
public static class BlockPlacementHelper
{
    /// <summary>
    /// Checks whether placing a block at blockPos would intersect an entity's axis-aligned bounding box.
    /// The entity position is assumed to be at the feet (y=bottom).
    /// </summary>
    public static bool IntersectsEntity(Vec3i blockPos, Vec3 entityFeetPos, float entityRadius = 0.35f, float entityHeight = 1.8f)
    {
        float blockMinX = blockPos.X;
        float blockMaxX = blockPos.X + 1.0f;
        float blockMinY = blockPos.Y;
        float blockMaxY = blockPos.Y + 1.0f;
        float blockMinZ = blockPos.Z;
        float blockMaxZ = blockPos.Z + 1.0f;

        float entMinX = entityFeetPos.X - entityRadius;
        float entMaxX = entityFeetPos.X + entityRadius;
        float entMinY = entityFeetPos.Y;
        float entMaxY = entityFeetPos.Y + entityHeight;
        float entMinZ = entityFeetPos.Z - entityRadius;
        float entMaxZ = entityFeetPos.Z + entityRadius;

        return blockMinX < entMaxX && blockMaxX > entMinX &&
               blockMinY < entMaxY && blockMaxY > entMinY &&
               blockMinZ < entMaxZ && blockMaxZ > entMinZ;
    }
}
