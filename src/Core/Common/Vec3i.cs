namespace VoxelKing.Core.Common;

/// <summary>
/// Immutable 3D integer vector representing discrete voxel coordinates and grid offsets.
/// </summary>
public readonly record struct Vec3i(int X, int Y, int Z) : IComparable<Vec3i>, IFormattable
{
    public static readonly Vec3i Zero = new(0, 0, 0);
    public static readonly Vec3i One = new(1, 1, 1);
    public static readonly Vec3i UnitX = new(1, 0, 0);
    public static readonly Vec3i UnitY = new(0, 1, 0);
    public static readonly Vec3i UnitZ = new(0, 0, 1);

    public static readonly Vec3i Up = new(0, 1, 0);
    public static readonly Vec3i Down = new(0, -1, 0);
    public static readonly Vec3i Left = new(-1, 0, 0);
    public static readonly Vec3i Right = new(1, 0, 0);
    public static readonly Vec3i Forward = new(0, 0, -1);
    public static readonly Vec3i Back = new(0, 0, 1);

    public static readonly Vec3i North = new(0, 0, -1);
    public static readonly Vec3i South = new(0, 0, 1);
    public static readonly Vec3i East = new(1, 0, 0);
    public static readonly Vec3i West = new(-1, 0, 0);

    public static readonly Vec3i[] Directions6 =
    [
        Up,
        Down,
        North,
        South,
        East,
        West,
    ];

    public Vec3i(int scalar) : this(scalar, scalar, scalar)
    {
    }

    public static Vec3i operator +(Vec3i a, Vec3i b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3i operator -(Vec3i a, Vec3i b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3i operator -(Vec3i a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3i operator *(Vec3i a, int scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
    public static Vec3i operator *(int scalar, Vec3i a) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
    public static Vec3i operator *(Vec3i a, Vec3i b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vec3i operator /(Vec3i a, int scalar) => new(a.X / scalar, a.Y / scalar, a.Z / scalar);
    public static Vec3i operator %(Vec3i a, int scalar) => new(a.X % scalar, a.Y % scalar, a.Z % scalar);

    public Vec3i Offset(int dx, int dy, int dz) => new(X + dx, Y + dy, Z + dz);
    public Vec3i Offset(Vec3i delta) => new(X + delta.X, Y + delta.Y, Z + delta.Z);

    public long DistanceSquared(Vec3i other)
    {
        long dx = (long)X - other.X;
        long dy = (long)Y - other.Y;
        long dz = (long)Z - other.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    public float Distance(Vec3i other) => MathF.Sqrt(DistanceSquared(other));

    public int ManhattanDistance(Vec3i other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

    public int ChebyshevDistance(Vec3i other) =>
        Math.Max(Math.Abs(X - other.X), Math.Max(Math.Abs(Y - other.Y), Math.Abs(Z - other.Z)));

    public Vec3 ToVec3() => new(X, Y, Z);

    public int CompareTo(Vec3i other)
    {
        int cx = X.CompareTo(other.X);
        if (cx != 0) return cx;
        int cy = Y.CompareTo(other.Y);
        if (cy != 0) return cy;
        return Z.CompareTo(other.Z);
    }

    public override string ToString() => ToString(null, System.Globalization.CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var provider = formatProvider ?? System.Globalization.CultureInfo.InvariantCulture;
        return $"({X.ToString(format, provider)}, {Y.ToString(format, provider)}, {Z.ToString(format, provider)})";
    }
}
