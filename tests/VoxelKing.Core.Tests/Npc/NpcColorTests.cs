namespace VoxelKing.Core.Tests.Npc;

using System;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcColorTests
{
    [Theory]
    [InlineData("#FF0000", 1f, 0f, 0f)]
    [InlineData("#00FF00", 0f, 1f, 0f)]
    [InlineData("#0000FF", 0f, 0f, 1f)]
    [InlineData("#3D7BEB", 0.2392f, 0.4823f, 0.9215f)]
    public void TryParse_Hex6_ParsesCorrectly(string hex, float expectedR, float expectedG, float expectedB)
    {
        bool success = NpcColor.TryParse(hex, out var color);
        Assert.True(success);
        Assert.InRange(color.R, expectedR - 0.01f, expectedR + 0.01f);
        Assert.InRange(color.G, expectedG - 0.01f, expectedG + 0.01f);
        Assert.InRange(color.B, expectedB - 0.01f, expectedB + 0.01f);
    }

    [Fact]
    public void TryParse_Hex3_ParsesCorrectly()
    {
        bool success = NpcColor.TryParse("#F00", out var color);
        Assert.True(success);
        Assert.InRange(color.R, 0.99f, 1.01f);
        Assert.InRange(color.G, -0.01f, 0.01f);
        Assert.InRange(color.B, -0.01f, 0.01f);
    }

    [Theory]
    [InlineData("red", 0.9f, 0.2f, 0.2f)]
    [InlineData("green", 0.2f, 0.8f, 0.3f)]
    [InlineData("blue", 0.2f, 0.45f, 0.9f)]
    [InlineData("purple", 0.6f, 0.3f, 0.8f)]
    public void TryParse_NamedColors_ParsesCorrectly(string name, float expectedR, float expectedG, float expectedB)
    {
        bool success = NpcColor.TryParse(name, out var color);
        Assert.True(success);
        Assert.InRange(color.R, expectedR - 0.01f, expectedR + 0.01f);
        Assert.InRange(color.G, expectedG - 0.01f, expectedG + 0.01f);
        Assert.InRange(color.B, expectedB - 0.01f, expectedB + 0.01f);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid_color_name_xyz")]
    [InlineData("#ZZZZZZ")]
    public void TryParse_InvalidOrNull_ReturnsFalseAndDefaultColor(string? input)
    {
        bool success = NpcColor.TryParse(input, out var color);
        Assert.False(success);
        Assert.Equal(NpcColor.DefaultShirt, color);
    }

    [Fact]
    public void ToHex_FormatsCorrectly()
    {
        var col = new NpcColor(1f, 0f, 0.5f);
        string hex = col.ToHex();
        Assert.Equal("#FF0080", hex);
    }
}
