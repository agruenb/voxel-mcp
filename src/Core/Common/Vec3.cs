namespace VoxelKing.Core.Common;

/// <summary>
/// Immutable 3D floating-point vector representing continuous spatial positions, velocities, and directions.
/// </summary>
public readonly record struct Vec3(float X, float Y, float Z) : IFormattable
{
    public static readonly Vec3 Zero = new(0f, 0f, 0f);
    public static readonly Vec3 One = new(1f, 1f, 1f);
    public static readonly Vec3 UnitX = new(1f, 0f, 0f);
    public static readonly Vec3 UnitY = new(0f, 1f, 0f);
    public static readonly Vec3 UnitZ = new(0f, 0f, 1f);

    public static readonly Vec3 Up = new(0f, 1f, 0f);
    public static readonly Vec3 Down = new(0f, -1f, 0f);
    public static readonly Vec3 Left = new(-1f, 0f, 0f);
    public static readonly Vec3 Right = new(1f, 0f, 0f);
    public static readonly Vec3 Forward = new(0f, 0f, -1f);
    public static readonly Vec3 Back = new(0f, 0f, 1f);

    public Vec3(float scalar) : this(scalar, scalar, scalar)
    {
    }

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, float scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
    public static Vec3 operator *(float scalar, Vec3 a) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
    public static Vec3 operator *(Vec3 a, Vec3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vec3 operator /(Vec3 a, float scalar) => new(a.X / scalar, a.Y / scalar, a.Z / scalar);
    public static Vec3 operator /(Vec3 a, Vec3 b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);

    public float LengthSquared() => (X * X) + (Y * Y) + (Z * Z);
    public float Length() => MathF.Sqrt(LengthSquared());

    public float DistanceSquared(Vec3 other) => (this - other).LengthSquared();
    public float Distance(Vec3 other) => MathF.Sqrt(DistanceSquared(other));

    public float Dot(Vec3 other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    public Vec3 Cross(Vec3 other) =>
        new((Y * other.Z) - (Z * other.Y), (Z * other.X) - (X * other.Z), (X * other.Y) - (Y * other.X));

    public Vec3 Normalized()
    {
        float lenSq = LengthSquared();
        if (lenSq <= 1e-12f)
        {
            return Zero;
        }

        float invLen = 1f / MathF.Sqrt(lenSq);
        return new Vec3(X * invLen, Y * invLen, Z * invLen);
    }

    public Vec3 Lerp(Vec3 other, float t) =>
        new(X + ((other.X - X) * t), Y + ((other.Y - Y) * t), Z + ((other.Z - Z) * t));

    public Vec3 Offset(float dx, float dy, float dz) => new(X + dx, Y + dy, Z + dz);
    public Vec3 Offset(Vec3 delta) => new(X + delta.X, Y + delta.Y, Z + delta.Z);

    public Vec3i FloorToInt() =>
        new((int)MathF.Floor(X), (int)MathF.Floor(Y), (int)MathF.Floor(Z));

    public Vec3i RoundToInt() =>
        new((int)MathF.Round(X), (int)MathF.Round(Y), (int)MathF.Round(Z));

    public Vec3i CeilToInt() =>
        new((int)MathF.Ceiling(X), (int)MathF.Ceiling(Y), (int)MathF.Ceiling(Z));

    public bool IsApproximatelyEqual(Vec3 other, float epsilon = 1e-5f) =>
        MathF.Abs(X - other.X) <= epsilon &&
        MathF.Abs(Y - other.Y) <= epsilon &&
        MathF.Abs(Z - other.Z) <= epsilon;

    public override string ToString() => ToString(null, System.Globalization.CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var provider = formatProvider ?? System.Globalization.CultureInfo.InvariantCulture;
        return $"({X.ToString(format, provider)}, {Y.ToString(format, provider)}, {Z.ToString(format, provider)})";
    }
}
