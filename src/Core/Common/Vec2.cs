namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// Immutable 2D floating-point vector representing texture coordinates and 2D spatial dimensions.
/// </summary>
public readonly record struct Vec2(float X, float Y) : IFormattable
{
    public static readonly Vec2 Zero = new(0f, 0f);
    public static readonly Vec2 One = new(1f, 1f);
    public static readonly Vec2 UnitX = new(1f, 0f);
    public static readonly Vec2 UnitY = new(0f, 1f);

    public Vec2(float scalar) : this(scalar, scalar)
    {
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vec2 operator *(float scalar, Vec2 a) => new(a.X * scalar, a.Y * scalar);
    public static Vec2 operator *(Vec2 a, Vec2 b) => new(a.X * b.X, a.Y * b.Y);
    public static Vec2 operator /(Vec2 a, float scalar) => new(a.X / scalar, a.Y / scalar);
    public static Vec2 operator /(Vec2 a, Vec2 b) => new(a.X / b.X, a.Y / b.Y);

    public float LengthSquared() => (X * X) + (Y * Y);
    public float Length() => MathF.Sqrt(LengthSquared());

    public Vec2 Normalized()
    {
        float lenSq = LengthSquared();
        if (lenSq <= 1e-12f)
        {
            return Zero;
        }

        float invLen = 1f / MathF.Sqrt(lenSq);
        return new Vec2(X * invLen, Y * invLen);
    }

    public Vec2 Lerp(Vec2 other, float t) =>
        new(X + ((other.X - X) * t), Y + ((other.Y - Y) * t));

    public bool IsApproximatelyEqual(Vec2 other, float epsilon = 1e-5f) =>
        MathF.Abs(X - other.X) <= epsilon &&
        MathF.Abs(Y - other.Y) <= epsilon;

    public override string ToString() => ToString(null, System.Globalization.CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var provider = formatProvider ?? System.Globalization.CultureInfo.InvariantCulture;
        return $"({X.ToString(format, provider)}, {Y.ToString(format, provider)})";
    }
}
