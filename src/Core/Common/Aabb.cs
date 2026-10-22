namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// Axis-aligned bounding box (AABB) in continuous 3D world space used for voxel swept collision.
/// </summary>
public readonly record struct Aabb(Vec3 Min, Vec3 Max)
{
    private const float Epsilon = 1e-4f;

    public Vec3 Size => Max - Min;
    public Vec3 Center => (Min + Max) * 0.5f;

    public static Aabb FromFeet(Vec3 feet, float width, float height)
    {
        float halfWidth = width * 0.5f;
        return new Aabb(
            new Vec3(feet.X - halfWidth, feet.Y, feet.Z - halfWidth),
            new Vec3(feet.X + halfWidth, feet.Y + height, feet.Z + halfWidth)
        );
    }

    public static Aabb FromBlock(Vec3i block)
    {
        return new Aabb(
            new Vec3(block.X, block.Y, block.Z),
            new Vec3(block.X + 1.0f, block.Y + 1.0f, block.Z + 1.0f)
        );
    }

    public Aabb Offset(Vec3 offset) => new(Min + offset, Max + offset);

    public Aabb Offset(float dx, float dy, float dz) =>
        new(new Vec3(Min.X + dx, Min.Y + dy, Min.Z + dz), new Vec3(Max.X + dx, Max.Y + dy, Max.Z + dz));

    public bool Intersects(Aabb other)
    {
        return Max.X > other.Min.X && Min.X < other.Max.X &&
               Max.Y > other.Min.Y && Min.Y < other.Max.Y &&
               Max.Z > other.Min.Z && Min.Z < other.Max.Z;
    }

    /// <summary>
    /// Clips displacement along the X axis so this bounding box does not penetrate the target box.
    /// </summary>
    public float ClipX(Aabb other, float dx)
    {
        if (Max.Y <= other.Min.Y || Min.Y >= other.Max.Y ||
            Max.Z <= other.Min.Z || Min.Z >= other.Max.Z)
        {
            return dx;
        }

        if (dx > 0f && Max.X <= other.Min.X + Epsilon)
        {
            float diff = other.Min.X - Max.X;
            if (diff < dx) dx = MathF.Max(0f, diff);
        }
        else if (dx < 0f && Min.X >= other.Max.X - Epsilon)
        {
            float diff = other.Max.X - Min.X;
            if (diff > dx) dx = MathF.Min(0f, diff);
        }

        return dx;
    }

    /// <summary>
    /// Clips displacement along the Y axis so this bounding box does not penetrate the target box.
    /// </summary>
    public float ClipY(Aabb other, float dy)
    {
        if (Max.X <= other.Min.X || Min.X >= other.Max.X ||
            Max.Z <= other.Min.Z || Min.Z >= other.Max.Z)
        {
            return dy;
        }

        if (dy > 0f && Max.Y <= other.Min.Y + Epsilon)
        {
            float diff = other.Min.Y - Max.Y;
            if (diff < dy) dy = MathF.Max(0f, diff);
        }
        else if (dy < 0f && Min.Y >= other.Max.Y - Epsilon)
        {
            float diff = other.Max.Y - Min.Y;
            if (diff > dy) dy = MathF.Min(0f, diff);
        }

        return dy;
    }

    /// <summary>
    /// Clips displacement along the Z axis so this bounding box does not penetrate the target box.
    /// </summary>
    public float ClipZ(Aabb other, float dz)
    {
        if (Max.X <= other.Min.X || Min.X >= other.Max.X ||
            Max.Y <= other.Min.Y || Min.Y >= other.Max.Y)
        {
            return dz;
        }

        if (dz > 0f && Max.Z <= other.Min.Z + Epsilon)
        {
            float diff = other.Min.Z - Max.Z;
            if (diff < dz) dz = MathF.Max(0f, diff);
        }
        else if (dz < 0f && Min.Z >= other.Max.Z - Epsilon)
        {
            float diff = other.Max.Z - Min.Z;
            if (diff > dz) dz = MathF.Min(0f, diff);
        }

        return dz;
    }
}
