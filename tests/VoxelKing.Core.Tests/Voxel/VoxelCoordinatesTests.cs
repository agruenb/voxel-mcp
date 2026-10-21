namespace VoxelKing.Core.Tests.Voxel;

using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using Xunit;

public class VoxelCoordinatesTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(31, 0, 31)]
    [InlineData(32, 1, 0)]
    [InlineData(63, 1, 31)]
    [InlineData(64, 2, 0)]
    public void WorldToChunkAndLocal_PositiveCoordinates(int worldCoord, int expectedChunk, int expectedLocal)
    {
        Assert.Equal(expectedChunk, VoxelCoordinates.WorldToChunk(worldCoord));
        Assert.Equal(expectedLocal, VoxelCoordinates.WorldToLocal(worldCoord));
    }

    [Theory]
    [InlineData(-1, -1, 31)]
    [InlineData(-32, -1, 0)]
    [InlineData(-33, -2, 31)]
    [InlineData(-64, -2, 0)]
    [InlineData(-65, -3, 31)]
    public void WorldToChunkAndLocal_NegativeCoordinates(int worldCoord, int expectedChunk, int expectedLocal)
    {
        Assert.Equal(expectedChunk, VoxelCoordinates.WorldToChunk(worldCoord));
        Assert.Equal(expectedLocal, VoxelCoordinates.WorldToLocal(worldCoord));
    }

    [Fact]
    public void WorldToChunkAndLocal_3DVectorConversions()
    {
        var worldPos = new Vec3i(-35, 10, 67);
        var (chunk, local) = VoxelCoordinates.WorldToChunkAndLocal(worldPos);

        // -35: chunk = -2, local = 29 (-2 * 32 + 29 = -64 + 29 = -35)
        // 10: chunk = 0, local = 10
        // 67: chunk = 2, local = 3 (2 * 32 + 3 = 64 + 3 = 67)
        Assert.Equal(new Vec3i(-2, 0, 2), chunk);
        Assert.Equal(new Vec3i(29, 10, 3), local);

        var reconstructed = VoxelCoordinates.LocalToWorld(chunk, local);
        Assert.Equal(worldPos, reconstructed);
    }

    [Fact]
    public void LocalToWorld_RoundTripsOverWideNegativeAndPositiveRange()
    {
        for (int x = -100; x <= 100; x += 7)
        {
            for (int y = -50; y <= 50; y += 9)
            {
                for (int z = -100; z <= 100; z += 11)
                {
                    var world = new Vec3i(x, y, z);
                    var (chunk, local) = VoxelCoordinates.WorldToChunkAndLocal(world);

                    Assert.InRange(local.X, 0, 31);
                    Assert.InRange(local.Y, 0, 31);
                    Assert.InRange(local.Z, 0, 31);

                    var backToWorld = VoxelCoordinates.LocalToWorld(chunk, local);
                    Assert.Equal(world, backToWorld);
                }
            }
        }
    }

    [Fact]
    public void IsOnBorder_DetectsAllSixChunkBoundaries()
    {
        // Interior
        Assert.False(VoxelCoordinates.IsOnBorder(new Vec3i(1, 1, 1)));
        Assert.False(VoxelCoordinates.IsOnBorder(new Vec3i(16, 16, 16)));
        Assert.False(VoxelCoordinates.IsOnBorder(new Vec3i(30, 30, 30)));

        // Borders
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(0, 16, 16)));
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(31, 16, 16)));
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(16, 0, 16)));
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(16, 31, 16)));
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(16, 16, 0)));
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(16, 16, 31)));

        // Corners
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(0, 0, 0)));
        Assert.True(VoxelCoordinates.IsOnBorder(new Vec3i(31, 31, 31)));
    }

    [Fact]
    public void GetBorderNeighborChunkOffsets_ReturnsCorrectDirections()
    {
        // Interior returns nothing
        Assert.Empty(VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(16, 16, 16)));

        // Individual faces
        Assert.Equal([Vec3i.Left], VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(0, 16, 16)).ToArray());
        Assert.Equal([Vec3i.Right], VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(31, 16, 16)).ToArray());
        Assert.Equal([Vec3i.Down], VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(16, 0, 16)).ToArray());
        Assert.Equal([Vec3i.Up], VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(16, 31, 16)).ToArray());
        Assert.Equal([Vec3i.North], VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(16, 16, 0)).ToArray());
        Assert.Equal([Vec3i.South], VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(16, 16, 31)).ToArray());

        // Corner (0, 0, 0) touches 3 orthogonal neighbors: -X (Left), -Y (Down), -Z (North)
        var cornerOffsets = VoxelCoordinates.GetBorderNeighborChunkOffsets(new Vec3i(0, 0, 0)).ToList();
        Assert.Equal(3, cornerOffsets.Count);
        Assert.Contains(Vec3i.Left, cornerOffsets);
        Assert.Contains(Vec3i.Down, cornerOffsets);
        Assert.Contains(Vec3i.North, cornerOffsets);
    }
}
