namespace VoxelKing.Core.Tests.Common;

using VoxelKing.Core.Common;
using Xunit;

public class AabbTests
{
    [Fact]
    public void FromFeet_Creates_Correct_Bounds()
    {
        var feet = new Vec3(10f, 5f, 20f);
        var aabb = Aabb.FromFeet(feet, width: 0.6f, height: 1.8f);

        Assert.Equal(9.7f, aabb.Min.X, 4);
        Assert.Equal(5.0f, aabb.Min.Y, 4);
        Assert.Equal(19.7f, aabb.Min.Z, 4);

        Assert.Equal(10.3f, aabb.Max.X, 4);
        Assert.Equal(6.8f, aabb.Max.Y, 4);
        Assert.Equal(20.3f, aabb.Max.Z, 4);

        Assert.True(aabb.Size.IsApproximatelyEqual(new Vec3(0.6f, 1.8f, 0.6f), 1e-4f));
        Assert.True(aabb.Center.IsApproximatelyEqual(new Vec3(10f, 5.9f, 20f), 1e-4f));
    }

    [Fact]
    public void FromBlock_Creates_Unit_Voxel_Bounds()
    {
        var block = new Vec3i(3, 7, -2);
        var aabb = Aabb.FromBlock(block);

        Assert.Equal(new Vec3(3f, 7f, -2f), aabb.Min);
        Assert.Equal(new Vec3(4f, 8f, -1f), aabb.Max);
    }

    [Fact]
    public void Intersects_Detects_Overlapping_And_NonOverlapping_Boxes()
    {
        var a = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(2f, 2f, 2f));
        var b = new Aabb(new Vec3(1f, 1f, 1f), new Vec3(3f, 3f, 3f));
        var c = new Aabb(new Vec3(2f, 0f, 0f), new Vec3(4f, 2f, 2f)); // Touching on face, not intersecting volume
        var d = new Aabb(new Vec3(5f, 5f, 5f), new Vec3(6f, 6f, 6f));

        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
        Assert.False(a.Intersects(c));
        Assert.False(a.Intersects(d));
    }

    [Fact]
    public void ClipX_Clips_Positive_And_Negative_Movement()
    {
        var moving = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(1f, 1f, 1f));
        var obstacle = new Aabb(new Vec3(2f, 0f, 0f), new Vec3(3f, 1f, 1f));

        // Moving right (+X) by 2.0 -> should clip to 1.0 (touching at X=2)
        float dx = moving.ClipX(obstacle, 2.0f);
        Assert.Equal(1.0f, dx, 4);

        // Moving away (-X) by -1.0 -> no clipping
        float dxAway = moving.ClipX(obstacle, -1.0f);
        Assert.Equal(-1.0f, dxAway, 4);

        // Moving left (-X) from X=4 against obstacle at X=2..3
        var movingLeft = new Aabb(new Vec3(4f, 0f, 0f), new Vec3(5f, 1f, 1f));
        float dxLeft = movingLeft.ClipX(obstacle, -2.0f);
        Assert.Equal(-1.0f, dxLeft, 4);
    }

    [Fact]
    public void ClipY_Clips_Falling_Displacement_On_Floor()
    {
        var entity = new Aabb(new Vec3(0f, 1.5f, 0f), new Vec3(1f, 3.3f, 1f));
        var floor = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(1f, 1.0f, 1f));

        // Falling by -1.0m -> floor top is at 1.0m, entity feet at 1.5m -> max fall is -0.5m
        float dy = entity.ClipY(floor, -1.0f);
        Assert.Equal(-0.5f, dy, 4);

        // Already standing at 1.0m, attempting to fall further
        var onFloor = new Aabb(new Vec3(0f, 1.0f, 0f), new Vec3(1f, 2.8f, 1f));
        float dyGrounded = onFloor.ClipY(floor, -0.2f);
        Assert.Equal(0.0f, dyGrounded, 4);
    }
}
