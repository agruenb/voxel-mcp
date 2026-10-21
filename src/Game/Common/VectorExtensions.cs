namespace VoxelKing.Game.Common;

using Godot;
using VoxelKing.Core.Common;

/// <summary>
/// Extension methods for converting between VoxelKing.Core math types and Godot vector types.
/// </summary>
public static class VectorExtensions
{
    public static Vector3 ToGodot(this Vec3 v) => new(v.X, v.Y, v.Z);

    public static Vector3I ToGodot(this Vec3i v) => new(v.X, v.Y, v.Z);

    public static Vector3 ToGodotVector3(this Vec3i v) => new(v.X, v.Y, v.Z);

    public static Vec3 ToCore(this Vector3 v) => new(v.X, v.Y, v.Z);

    public static Vec3i ToCore(this Vector3I v) => new(v.X, v.Y, v.Z);

    public static Vec3i ToCoreVec3i(this Vector3 v) =>
        new(Mathf.FloorToInt(v.X), Mathf.FloorToInt(v.Y), Mathf.FloorToInt(v.Z));
}
