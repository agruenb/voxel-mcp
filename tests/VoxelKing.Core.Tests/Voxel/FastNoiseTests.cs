namespace VoxelKing.Core.Tests.Voxel;

using VoxelKing.Core.Voxel;
using Xunit;

public class FastNoiseTests
{
    [Fact]
    public void Noise2D_SameSeedProducesIdenticalValues()
    {
        var noise1 = new FastNoise(1337);
        var noise2 = new FastNoise(1337);

        for (int x = -50; x <= 50; x += 5)
        {
            for (int y = -50; y <= 50; y += 5)
            {
                float v1 = noise1.GetNoise2D(x * 0.1f, y * 0.1f);
                float v2 = noise2.GetNoise2D(x * 0.1f, y * 0.1f);
                Assert.Equal(v1, v2);
                Assert.InRange(v1, -1.05f, 1.05f);
            }
        }
    }

    [Fact]
    public void Noise2D_DifferentSeedsProduceDifferentValues()
    {
        var noise1 = new FastNoise(1337);
        var noise2 = new FastNoise(9999);

        bool foundDifference = false;
        for (int x = 0; x < 10; x++)
        {
            float v1 = noise1.GetNoise2D(x * 0.1f, 0.5f);
            float v2 = noise2.GetNoise2D(x * 0.1f, 0.5f);
            if (Math.Abs(v1 - v2) > 0.001f)
            {
                foundDifference = true;
                break;
            }
        }

        Assert.True(foundDifference);
    }

    [Fact]
    public void Noise3D_SameSeedProducesIdenticalValues()
    {
        var noise1 = new FastNoise(42);
        var noise2 = new FastNoise(42);

        for (int x = -10; x <= 10; x += 5)
        {
            for (int y = -10; y <= 10; y += 5)
            {
                for (int z = -10; z <= 10; z += 5)
                {
                    float v1 = noise1.GetNoise3D(x * 0.1f, y * 0.1f, z * 0.1f);
                    float v2 = noise2.GetNoise3D(x * 0.1f, y * 0.1f, z * 0.1f);
                    Assert.Equal(v1, v2);
                    Assert.InRange(v1, -1.05f, 1.05f);
                }
            }
        }
    }

    [Fact]
    public void Fbm2D_ReturnsNormalizedValues()
    {
        var noise = new FastNoise(12345);

        for (int x = -100; x <= 100; x += 20)
        {
            for (int y = -100; y <= 100; y += 20)
            {
                float fbm = noise.GetFbm2D(x, y, octaves: 4, persistence: 0.5f, lacunarity: 2.0f, frequency: 0.01f);
                Assert.InRange(fbm, -1.1f, 1.1f);
            }
        }
    }
}
