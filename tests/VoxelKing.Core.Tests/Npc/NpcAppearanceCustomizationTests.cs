namespace VoxelKing.Core.Tests.Npc;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcAppearanceCustomizationTests
{
    [Theory]
    [InlineData("farmer", NpcArchetype.Farmer)]
    [InlineData("Miner", NpcArchetype.Miner)]
    [InlineData("GUARD", NpcArchetype.Guard)]
    [InlineData("woodcutter", NpcArchetype.Woodcutter)]
    [InlineData("courier", NpcArchetype.Courier)]
    [InlineData("explorer", NpcArchetype.Explorer)]
    [InlineData("villager", NpcArchetype.Villager)]
    public void ResolveArchetype_ExplicitArchetype_ReturnsExactArchetype(string explicitTag, NpcArchetype expected)
    {
        var result = NpcAppearanceCustomization.ResolveArchetype(explicitTag, null, null, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("farming", NpcArchetype.Farmer)]
    [InlineData("woodcutting", NpcArchetype.Woodcutter)]
    [InlineData("quarrying", NpcArchetype.Miner)]
    [InlineData("patrolling", NpcArchetype.Guard)]
    [InlineData("delivery", NpcArchetype.Courier)]
    [InlineData("exploration", NpcArchetype.Explorer)]
    public void ResolveArchetype_SkillsInference_ReturnsMatchingArchetype(string skill, NpcArchetype expected)
    {
        var result = NpcAppearanceCustomization.ResolveArchetype(null, "TestNPC", new[] { skill }, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("grumpy but helpful farmer", NpcArchetype.Farmer)]
    [InlineData("energetic woodcutter", NpcArchetype.Woodcutter)]
    [InlineData("diligent architect constructing stone fortifications", NpcArchetype.Miner)]
    [InlineData("vigilant village guard keeping watch", NpcArchetype.Guard)]
    [InlineData("dependable courier delivering supplies", NpcArchetype.Courier)]
    [InlineData("inquisitive explorer surveying mountains", NpcArchetype.Explorer)]
    public void ResolveArchetype_PersonalityInference_ReturnsMatchingArchetype(string personality, NpcArchetype expected)
    {
        var result = NpcAppearanceCustomization.ResolveArchetype(null, "CustomNPC", null, personality);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Bob", NpcArchetype.Farmer)]
    [InlineData("Alice", NpcArchetype.Woodcutter)]
    [InlineData("Charlie", NpcArchetype.Miner)]
    [InlineData("Diana", NpcArchetype.Guard)]
    [InlineData("Edward", NpcArchetype.Courier)]
    [InlineData("Fiona", NpcArchetype.Explorer)]
    [InlineData("UnknownGuy", NpcArchetype.Villager)]
    public void ResolveArchetype_NameFallback_MatchesDefaultCast(string name, NpcArchetype expected)
    {
        var result = NpcAppearanceCustomization.ResolveArchetype(null, name, null, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(NpcArchetype.Farmer, NpcHeadgearType.StrawHat)]
    [InlineData(NpcArchetype.Miner, NpcHeadgearType.MinerHelmet)]
    [InlineData(NpcArchetype.Guard, NpcHeadgearType.Headband)]
    [InlineData(NpcArchetype.Courier, NpcHeadgearType.Cap)]
    [InlineData(NpcArchetype.Woodcutter, NpcHeadgearType.Headband)]
    [InlineData(NpcArchetype.Explorer, NpcHeadgearType.Headband)]
    [InlineData(NpcArchetype.Villager, NpcHeadgearType.HairOnly)]
    public void GetHeadgearForArchetype_MapsCorrectSilhouette(NpcArchetype archetype, NpcHeadgearType expected)
    {
        var headgear = NpcAppearanceCustomization.GetHeadgearForArchetype(archetype);
        Assert.Equal(expected, headgear);
    }

    [Fact]
    public void ResolveAppearanceDetails_Farmer_ProducesStrawHatAndBoots()
    {
        var details = NpcAppearanceCustomization.ResolveAppearanceDetails(
            explicitArchetype: null,
            name: "Bob",
            skills: new[] { "farming", "building" },
            personality: "farmer",
            appearanceColor: "#3D7BEB");

        Assert.Equal(NpcArchetype.Farmer, details.Archetype);
        Assert.Equal(NpcHeadgearType.StrawHat, details.Headgear);
        Assert.Equal(NpcAppearanceCustomization.StrawHatBase, details.HeadgearColor);
        Assert.Equal(NpcAppearanceCustomization.StrawHatRibbon, details.HeadgearAccentColor);
        Assert.Equal(NpcAppearanceCustomization.DefaultBoots, details.BootsColor);
        Assert.Equal(NpcAppearanceCustomization.BrownTrousers, details.PantsColor);
    }

    [Fact]
    public void ResolveAppearanceDetails_Miner_ProducesMinerHelmetAndBoots()
    {
        var details = NpcAppearanceCustomization.ResolveAppearanceDetails(
            explicitArchetype: "miner",
            name: "Charlie",
            skills: new[] { "masonry", "mining" },
            personality: null,
            appearanceColor: "#E74C3C");

        Assert.Equal(NpcArchetype.Miner, details.Archetype);
        Assert.Equal(NpcHeadgearType.MinerHelmet, details.Headgear);
        Assert.Equal(NpcAppearanceCustomization.MinerHelmetBase, details.HeadgearColor);
        Assert.Equal(NpcAppearanceCustomization.MinerLampLight, details.HeadgearAccentColor);
        Assert.Equal(NpcAppearanceCustomization.MinerBoots, details.BootsColor);
    }

    [Fact]
    public void ResolveAppearanceDetails_CustomColor_CalculatesShirtAndCuffsHarmoniously()
    {
        var details = NpcAppearanceCustomization.ResolveAppearanceDetails(
            explicitArchetype: null,
            name: "Alice",
            skills: new[] { "woodcutting" },
            personality: null,
            appearanceColor: "#2ECC71");

        Assert.True(NpcColor.TryParse("#2ECC71", out var expectedShirt));
        Assert.Equal(expectedShirt.R, details.ShirtColor.R, precision: 2);
        Assert.Equal(expectedShirt.G, details.ShirtColor.G, precision: 2);
        Assert.Equal(expectedShirt.B, details.ShirtColor.B, precision: 2);

        // Cuffs should be non-zero and slightly lighter/tinted
        Assert.True(details.CuffsColor.R > 0f);
        Assert.True(details.CuffsColor.G > 0f);
    }
}
