namespace VoxelKing.Core.Npc;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Swept AABB kinematic mover resolving gravity, voxel collisions, and automated 1-block step climbing in Core.
/// </summary>
public static class KinematicMover
{
    private const float Epsilon = 1e-4f;

    /// <summary>
    /// Updates the kinematic movement of an NPC against the voxel world.
    /// </summary>
    public static void Move(
        NpcState npc,
        IVoxelWorld world,
        float dt,
        float gravity = 20.0f,
        float? stepHeight = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(world);

        if (dt <= 0f)
        {
            return;
        }

        // 1. Apply gravity
        npc.Velocity = npc.Velocity with { Y = npc.Velocity.Y - (gravity * dt) };

        float desiredDx = npc.Velocity.X * dt;
        float desiredDy = npc.Velocity.Y * dt;
        float desiredDz = npc.Velocity.Z * dt;

        float effectiveStep = stepHeight ?? npc.StepHeight;
        var originalBox = npc.GetBoundingBox();

        // 2. Compute broadphase search area
        float minSweptX = MathF.Min(originalBox.Min.X, originalBox.Min.X + desiredDx);
        float maxSweptX = MathF.Max(originalBox.Max.X, originalBox.Max.X + desiredDx);
        float minSweptY = MathF.Min(originalBox.Min.Y, originalBox.Min.Y + desiredDy);
        float maxSweptY = MathF.Max(originalBox.Max.Y, originalBox.Max.Y + desiredDy + (npc.IsOnGround ? effectiveStep : 0f));
        float minSweptZ = MathF.Min(originalBox.Min.Z, originalBox.Min.Z + desiredDz);
        float maxSweptZ = MathF.Max(originalBox.Max.Z, originalBox.Max.Z + desiredDz);

        int minX = (int)MathF.Floor(minSweptX - Epsilon);
        int maxX = (int)MathF.Floor(maxSweptX + Epsilon);
        int minY = (int)MathF.Floor(minSweptY - Epsilon);
        int maxY = (int)MathF.Floor(maxSweptY + Epsilon);
        int minZ = (int)MathF.Floor(minSweptZ - Epsilon);
        int maxZ = (int)MathF.Floor(maxSweptZ + Epsilon);

        var solidBoxes = new List<Aabb>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    ushort blockId = world.GetBlock(x, y, z);
                    if (blockId != 0 && world.BlockRegistry.TryGetDefinition(blockId, out var def) && def.IsSolid)
                    {
                        solidBoxes.Add(Aabb.FromBlock(new Vec3i(x, y, z)));
                    }
                }
            }
        }

        // 3. Resolve vertical movement (Y)
        float dy = desiredDy;
        for (int i = 0; i < solidBoxes.Count; i++)
        {
            dy = originalBox.ClipY(solidBoxes[i], dy);
        }

        var yBox = originalBox.Offset(0f, dy, 0f);

        bool hitFloor = desiredDy < 0f && dy > desiredDy;
        bool hitCeiling = desiredDy > 0f && dy < desiredDy;

        if (hitFloor)
        {
            npc.IsOnGround = true;
            npc.Velocity = npc.Velocity with { Y = 0f };
        }
        else if (hitCeiling)
        {
            npc.Velocity = npc.Velocity with { Y = 0f };
        }
        else if (desiredDy < 0f)
        {
            npc.IsOnGround = false;
        }

        // 4. Resolve horizontal movement (X and Z)
        float flatDx = desiredDx;
        float flatDz = desiredDz;

        var flatBox = yBox;
        for (int i = 0; i < solidBoxes.Count; i++)
        {
            flatDx = flatBox.ClipX(solidBoxes[i], flatDx);
        }
        flatBox = flatBox.Offset(flatDx, 0f, 0f);

        for (int i = 0; i < solidBoxes.Count; i++)
        {
            flatDz = flatBox.ClipZ(solidBoxes[i], flatDz);
        }
        flatBox = flatBox.Offset(0f, 0f, flatDz);

        bool hitWall = MathF.Abs(flatDx - desiredDx) > Epsilon || MathF.Abs(flatDz - desiredDz) > Epsilon;
        Aabb finalBox = flatBox;
        npc.IsOnWall = hitWall;

        // 5. Test Step Assist if on ground and blocked horizontally
        if (npc.IsOnGround && hitWall && effectiveStep > 0f && (MathF.Abs(desiredDx) > Epsilon || MathF.Abs(desiredDz) > Epsilon))
        {
            // Step path: Move UP -> Move HORIZONTAL -> Move DOWN
            float stepUp = effectiveStep;
            for (int i = 0; i < solidBoxes.Count; i++)
            {
                stepUp = yBox.ClipY(solidBoxes[i], stepUp);
            }
            var steppedBox = yBox.Offset(0f, stepUp, 0f);

            float stepDx = desiredDx;
            float stepDz = desiredDz;

            for (int i = 0; i < solidBoxes.Count; i++)
            {
                stepDx = steppedBox.ClipX(solidBoxes[i], stepDx);
            }
            steppedBox = steppedBox.Offset(stepDx, 0f, 0f);

            for (int i = 0; i < solidBoxes.Count; i++)
            {
                stepDz = steppedBox.ClipZ(solidBoxes[i], stepDz);
            }
            steppedBox = steppedBox.Offset(0f, 0f, stepDz);

            float stepDown = -stepUp;
            for (int i = 0; i < solidBoxes.Count; i++)
            {
                stepDown = steppedBox.ClipY(solidBoxes[i], stepDown);
            }
            steppedBox = steppedBox.Offset(0f, stepDown, 0f);

            float flatDistSq = (flatDx * flatDx) + (flatDz * flatDz);
            float stepDistSq = (stepDx * stepDx) + (stepDz * stepDz);

            // If stepped move achieved more horizontal progress, accept it
            if (stepDistSq > flatDistSq + 1e-5f)
            {
                finalBox = steppedBox;
                npc.IsOnGround = true;
                npc.IsOnWall = false;
            }
        }

        // 6. Final position update
        npc.Position = new Vec3(finalBox.Center.X, finalBox.Min.Y, finalBox.Center.Z);
    }
}
