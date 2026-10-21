namespace VoxelKing.Core.Tests.Common;

using VoxelKing.Core.Common;
using Xunit;

public class Vec3iTests
{
    [Fact]
    public void EqualityAndHashing_WorkCorrectly()
    {
        var a = new Vec3i(1, -2, 3);
        var b = new Vec3i(1, -2, 3);
        var c = new Vec3i(2, -2, 3);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var set = new HashSet<Vec3i> { a };
        Assert.Contains(b, set);
        Assert.DoesNotContain(c, set);
    }

    [Fact]
    public void ArithmeticOperators_CalculateExpectedValues()
    {
        var a = new Vec3i(10, 20, 30);
        var b = new Vec3i(1, 2, 3);

        Assert.Equal(new Vec3i(11, 22, 33), a + b);
        Assert.Equal(new Vec3i(9, 18, 27), a - b);
        Assert.Equal(new Vec3i(-10, -20, -30), -a);
        Assert.Equal(new Vec3i(20, 40, 60), a * 2);
        Assert.Equal(new Vec3i(20, 40, 60), 2 * a);
        Assert.Equal(new Vec3i(10, 40, 90), a * b);
        Assert.Equal(new Vec3i(5, 10, 15), a / 2);
        Assert.Equal(new Vec3i(1, 2, 0), new Vec3i(7, 8, 9) % 3);
    }

    [Fact]
    public void DistanceAndOffsets_CalculateCorrectly()
    {
        var a = new Vec3i(1, 2, 3);
        var b = new Vec3i(4, 6, 3); // dx=3, dy=4, dz=0 => dist=5

        Assert.Equal(25L, a.DistanceSquared(b));
        Assert.Equal(5f, a.Distance(b));
        Assert.Equal(7, a.ManhattanDistance(b));
        Assert.Equal(4, a.ChebyshevDistance(b));

        Assert.Equal(new Vec3i(2, 4, 6), a.Offset(1, 2, 3));
        Assert.Equal(new Vec3i(5, 8, 6), a.Offset(new Vec3i(4, 6, 3)));
    }

    [Fact]
    public void ComparisonAndSorting_OrdersLexicographically()
    {
        var list = new List<Vec3i>
        {
            new(2, 0, 0),
            new(1, 2, 0),
            new(1, 1, 5),
            new(1, 1, 2),
            new(0, 5, 5),
        };

        list.Sort();

        Assert.Equal(new Vec3i(0, 5, 5), list[0]);
        Assert.Equal(new Vec3i(1, 1, 2), list[1]);
        Assert.Equal(new Vec3i(1, 1, 5), list[2]);
        Assert.Equal(new Vec3i(1, 2, 0), list[3]);
        Assert.Equal(new Vec3i(2, 0, 0), list[4]);
    }

    [Fact]
    public void ConversionsAndToString_WorkAsExpected()
    {
        var v = new Vec3i(1, 2, 3);
        Assert.Equal("(1, 2, 3)", v.ToString());
        Assert.Equal(new Vec3(1f, 2f, 3f), v.ToVec3());
    }

    [Fact]
    public void Directions6_ContainsAllCardinalDirections()
    {
        Assert.Equal(6, Vec3i.Directions6.Length);
        Assert.Contains(Vec3i.Up, Vec3i.Directions6);
        Assert.Contains(Vec3i.Down, Vec3i.Directions6);
        Assert.Contains(Vec3i.North, Vec3i.Directions6);
        Assert.Contains(Vec3i.South, Vec3i.Directions6);
        Assert.Contains(Vec3i.East, Vec3i.Directions6);
        Assert.Contains(Vec3i.West, Vec3i.Directions6);
    }
}
