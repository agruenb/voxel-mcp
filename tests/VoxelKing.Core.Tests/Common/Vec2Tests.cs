namespace VoxelKing.Core.Tests.Common;

using System;
using VoxelKing.Core.Common;
using Xunit;

public class Vec2Tests
{
    [Fact]
    public void Arithmetic_CalculatesCorrectResults()
    {
        var a = new Vec2(2f, 4f);
        var b = new Vec2(1f, 2f);

        Assert.Equal(new Vec2(3f, 6f), a + b);
        Assert.Equal(new Vec2(1f, 2f), a - b);
        Assert.Equal(new Vec2(-2f, -4f), -a);
        Assert.Equal(new Vec2(4f, 8f), a * 2f);
        Assert.Equal(new Vec2(4f, 8f), 2f * a);
        Assert.Equal(new Vec2(2f, 8f), a * b);
        Assert.Equal(new Vec2(1f, 2f), a / 2f);
        Assert.Equal(new Vec2(2f, 2f), a / b);
    }

    [Fact]
    public void LengthAndNormalization_WorksExpectedly()
    {
        var v = new Vec2(3f, 4f);
        Assert.Equal(25f, v.LengthSquared());
        Assert.Equal(5f, v.Length());

        var norm = v.Normalized();
        Assert.True(norm.IsApproximatelyEqual(new Vec2(0.6f, 0.8f)));

        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized());
    }

    [Fact]
    public void LerpAndToString_WorksCorrectly()
    {
        var a = new Vec2(0f, 10f);
        var b = new Vec2(10f, 20f);

        var mid = a.Lerp(b, 0.5f);
        Assert.Equal(new Vec2(5f, 15f), mid);

        Assert.Equal("(5, 15)", mid.ToString());
    }
}
