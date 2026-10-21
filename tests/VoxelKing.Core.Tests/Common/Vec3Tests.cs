namespace VoxelKing.Core.Tests.Common;

using VoxelKing.Core.Common;
using Xunit;

public class Vec3Tests
{
    [Fact]
    public void EqualityAndHashing_WorkCorrectly()
    {
        var a = new Vec3(1.5f, -2.5f, 3.5f);
        var b = new Vec3(1.5f, -2.5f, 3.5f);
        var c = new Vec3(1.5f, 2.5f, 3.5f);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
        Assert.True(a.IsApproximatelyEqual(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Arithmetic_CalculatesExpectedValues()
    {
        var a = new Vec3(2f, 4f, 6f);
        var b = new Vec3(1f, 2f, 3f);

        Assert.Equal(new Vec3(3f, 6f, 9f), a + b);
        Assert.Equal(new Vec3(1f, 2f, 3f), a - b);
        Assert.Equal(new Vec3(-2f, -4f, -6f), -a);
        Assert.Equal(new Vec3(4f, 8f, 12f), a * 2f);
        Assert.Equal(new Vec3(4f, 8f, 12f), 2f * a);
        Assert.Equal(new Vec3(2f, 8f, 18f), a * b);
        Assert.Equal(new Vec3(1f, 2f, 3f), a / 2f);
        Assert.Equal(new Vec3(2f, 2f, 2f), a / b);
    }

    [Fact]
    public void VectorGeometry_LengthNormalizeDotCross()
    {
        var v = new Vec3(3f, 0f, 4f);
        Assert.Equal(25f, v.LengthSquared());
        Assert.Equal(5f, v.Length());

        var normalized = v.Normalized();
        Assert.True(normalized.IsApproximatelyEqual(new Vec3(0.6f, 0f, 0.8f)));
        Assert.True(MathF.Abs(normalized.Length() - 1f) < 1e-5f);

        Assert.Equal(Vec3.Zero, Vec3.Zero.Normalized());

        var right = new Vec3(1f, 0f, 0f);
        var up = new Vec3(0f, 1f, 0f);
        Assert.Equal(0f, right.Dot(up));
        Assert.Equal(1f, right.Dot(right));

        // Cross product: Right x Up = Back (0, 0, 1) in right-handed coordinate system
        Assert.Equal(new Vec3(0f, 0f, 1f), right.Cross(up));
    }

    [Fact]
    public void DistanceAndLerp_WorkCorrectly()
    {
        var a = new Vec3(0f, 0f, 0f);
        var b = new Vec3(0f, 10f, 0f);

        Assert.Equal(100f, a.DistanceSquared(b));
        Assert.Equal(10f, a.Distance(b));

        var mid = a.Lerp(b, 0.5f);
        Assert.Equal(new Vec3(0f, 5f, 0f), mid);
    }

    [Fact]
    public void DiscreteConversions_HandleNegativeCoordinatesProperly()
    {
        var pos = new Vec3(1.7f, -1.2f, -0.8f);

        Assert.Equal(new Vec3i(1, -2, -1), pos.FloorToInt());
        Assert.Equal(new Vec3i(2, -1, -1), pos.RoundToInt());
        Assert.Equal(new Vec3i(2, -1, 0), pos.CeilToInt());
    }

    [Fact]
    public void ToString_FormatsNicely()
    {
        var v = new Vec3(1.5f, 2.5f, 3.5f);
        Assert.Equal("(1.5, 2.5, 3.5)", v.ToString());
    }
}
