namespace VoxelKing.Core.Npc;

using System;
using System.Collections.Generic;

/// <summary>
/// Character archetype defining distinct clothing, silhouette, and headgear roles (F-210 Phase 4).
/// </summary>
public enum NpcArchetype
{
    Villager,
    Farmer,
    Woodcutter,
    Miner,
    Guard,
    Courier,
    Explorer
}

/// <summary>
/// Headgear / hat styles providing distinct silhouettes across NPC archetypes.
/// </summary>
public enum NpcHeadgearType
{
    None,
    HairOnly,
    StrawHat,
    MinerHelmet,
    Headband,
    Cap
}

/// <summary>
/// Resolved appearance configuration including hair, headgear, boots, and clothing colors.
/// </summary>
public readonly record struct NpcAppearanceDetails(
    NpcArchetype Archetype,
    NpcHeadgearType Headgear,
    NpcColor HairColor,
    NpcColor HeadgearColor,
    NpcColor HeadgearAccentColor,
    NpcColor ShirtColor,
    NpcColor PantsColor,
    NpcColor BootsColor,
    NpcColor CuffsColor);

/// <summary>
/// Pure logic and color math for NPC archetype customization and distinct silhouettes (F-210 Phase 4).
/// Free of any Godot dependencies and fully testable in Core.
/// </summary>
public static class NpcAppearanceCustomization
{
    // Preset Hair Colors
    public static readonly NpcColor HairChestnut = new(0.29f, 0.18f, 0.09f); // #4A2E18
    public static readonly NpcColor HairAuburn   = new(0.58f, 0.23f, 0.09f); // #943B17
    public static readonly NpcColor HairDark     = new(0.16f, 0.16f, 0.16f); // #292929
    public static readonly NpcColor HairBlonde   = new(0.78f, 0.59f, 0.24f); // #C7963D
    public static readonly NpcColor HairRaven    = new(0.11f, 0.11f, 0.12f); // #1C1C1F
    public static readonly NpcColor HairSilver   = new(0.75f, 0.75f, 0.78f); // #BFC0C7

    // Clothing Accent Colors
    public static readonly NpcColor DefaultBoots   = new(0.20f, 0.13f, 0.08f); // Sturdy dark leather
    public static readonly NpcColor GuardBoots     = new(0.15f, 0.18f, 0.22f); // Steel-plated boots
    public static readonly NpcColor MinerBoots     = new(0.18f, 0.16f, 0.14f); // Reinforced work boots
    public static readonly NpcColor DefaultCuffs   = new(0.88f, 0.88f, 0.88f); // Linen cuff
    public static readonly NpcColor DefaultPants   = new(0.18f, 0.22f, 0.32f); // Denim pants
    public static readonly NpcColor BrownTrousers  = new(0.28f, 0.22f, 0.16f); // Earthy trousers

    // Headgear Colors
    public static readonly NpcColor StrawHatBase   = new(0.87f, 0.72f, 0.53f); // #DEB887 Straw
    public static readonly NpcColor StrawHatRibbon = new(0.55f, 0.00f, 0.00f); // #8C0000 Crimson ribbon
    public static readonly NpcColor MinerHelmetBase = new(0.95f, 0.77f, 0.06f); // #F3C50F Yellow safety
    public static readonly NpcColor MinerLampLight  = new(1.00f, 0.98f, 0.65f); // Bright headlamp
    public static readonly NpcColor CapBase        = new(0.36f, 0.25f, 0.22f); // Brown cap
    public static readonly NpcColor CapVisor       = new(0.24f, 0.15f, 0.14f); // Dark visor brim

    /// <summary>
    /// Resolves an NPC's archetype from profile data (explicit archetype tag, skills, personality, or name).
    /// </summary>
    public static NpcArchetype ResolveArchetype(
        string? explicitArchetype,
        string? name,
        IReadOnlyList<string>? skills,
        string? personality)
    {
        if (!string.IsNullOrWhiteSpace(explicitArchetype))
        {
            if (Enum.TryParse<NpcArchetype>(explicitArchetype.Trim(), ignoreCase: true, out var parsed))
            {
                return parsed;
            }
        }

        // 1. Inspect skills
        if (skills != null)
        {
            foreach (var skill in skills)
            {
                if (skill.Contains("farm", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("harvest", StringComparison.OrdinalIgnoreCase))
                {
                    return NpcArchetype.Farmer;
                }
                if (skill.Contains("wood", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("chop", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("forag", StringComparison.OrdinalIgnoreCase))
                {
                    return NpcArchetype.Woodcutter;
                }
                if (skill.Contains("mine", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("mason", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("quarry", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("architect", StringComparison.OrdinalIgnoreCase))
                {
                    return NpcArchetype.Miner;
                }
                if (skill.Contains("guard", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("patrol", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("defend", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("scout", StringComparison.OrdinalIgnoreCase))
                {
                    return NpcArchetype.Guard;
                }
                if (skill.Contains("haul", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("deliver", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("courier", StringComparison.OrdinalIgnoreCase))
                {
                    return NpcArchetype.Courier;
                }
                if (skill.Contains("explor", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("follow", StringComparison.OrdinalIgnoreCase) ||
                    skill.Contains("pathfind", StringComparison.OrdinalIgnoreCase))
                {
                    return NpcArchetype.Explorer;
                }
            }
        }

        // 2. Inspect personality description
        if (!string.IsNullOrWhiteSpace(personality))
        {
            if (personality.Contains("farmer", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Farmer;
            if (personality.Contains("woodcutter", StringComparison.OrdinalIgnoreCase) || personality.Contains("gatherer", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Woodcutter;
            if (personality.Contains("mason", StringComparison.OrdinalIgnoreCase) || personality.Contains("miner", StringComparison.OrdinalIgnoreCase) || personality.Contains("architect", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Miner;
            if (personality.Contains("guard", StringComparison.OrdinalIgnoreCase) || personality.Contains("vigilant", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Guard;
            if (personality.Contains("courier", StringComparison.OrdinalIgnoreCase) || personality.Contains("delivery", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Courier;
            if (personality.Contains("explorer", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Explorer;
        }

        // 3. Known default name fallback
        if (!string.IsNullOrWhiteSpace(name))
        {
            string n = name.Trim();
            if (n.Equals("Bob", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Farmer;
            if (n.Equals("Alice", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Woodcutter;
            if (n.Equals("Charlie", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Miner;
            if (n.Equals("Diana", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Guard;
            if (n.Equals("Edward", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Courier;
            if (n.Equals("Fiona", StringComparison.OrdinalIgnoreCase)) return NpcArchetype.Explorer;
        }

        return NpcArchetype.Villager;
    }

    /// <summary>
    /// Gets the silhouette headgear associated with the given archetype.
    /// </summary>
    public static NpcHeadgearType GetHeadgearForArchetype(NpcArchetype archetype)
    {
        return archetype switch
        {
            NpcArchetype.Farmer => NpcHeadgearType.StrawHat,
            NpcArchetype.Miner => NpcHeadgearType.MinerHelmet,
            NpcArchetype.Guard => NpcHeadgearType.Headband,
            NpcArchetype.Courier => NpcHeadgearType.Cap,
            NpcArchetype.Woodcutter => NpcHeadgearType.Headband,
            NpcArchetype.Explorer => NpcHeadgearType.Headband,
            _ => NpcHeadgearType.HairOnly
        };
    }

    /// <summary>
    /// Resolves full appearance details (archetype, headgear, hair, boots, and clothing colors) for an NPC.
    /// </summary>
    public static NpcAppearanceDetails ResolveAppearanceDetails(
        string? explicitArchetype,
        string? name,
        IReadOnlyList<string>? skills,
        string? personality,
        string? appearanceColor)
    {
        var archetype = ResolveArchetype(explicitArchetype, name, skills, personality);
        var headgear = GetHeadgearForArchetype(archetype);

        // Resolve shirt color from appearanceColor if valid, else archetype fallback
        NpcColor shirtColor;
        if (!string.IsNullOrWhiteSpace(appearanceColor) && NpcColor.TryParse(appearanceColor, out var parsedShirt))
        {
            shirtColor = parsedShirt;
        }
        else
        {
            shirtColor = archetype switch
            {
                NpcArchetype.Farmer => new NpcColor(0.24f, 0.48f, 0.82f),
                NpcArchetype.Woodcutter => new NpcColor(0.18f, 0.65f, 0.35f),
                NpcArchetype.Miner => new NpcColor(0.85f, 0.30f, 0.24f),
                NpcArchetype.Guard => new NpcColor(0.55f, 0.32f, 0.68f),
                NpcArchetype.Courier => new NpcColor(0.90f, 0.58f, 0.12f),
                NpcArchetype.Explorer => new NpcColor(0.12f, 0.70f, 0.60f),
                _ => NpcColor.DefaultShirt
            };
        }

        // Distinct hair colors tailored to archetype and characters
        NpcColor hairColor = archetype switch
        {
            NpcArchetype.Farmer => HairChestnut,
            NpcArchetype.Woodcutter => HairAuburn,
            NpcArchetype.Miner => HairDark,
            NpcArchetype.Guard => HairDark,
            NpcArchetype.Courier => HairBlonde,
            NpcArchetype.Explorer => HairRaven,
            _ => HairChestnut
        };

        // Specific overrides for default cast
        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Equals("Alice", StringComparison.OrdinalIgnoreCase)) hairColor = HairAuburn;
            else if (name.Equals("Bob", StringComparison.OrdinalIgnoreCase)) hairColor = HairChestnut;
            else if (name.Equals("Charlie", StringComparison.OrdinalIgnoreCase)) hairColor = HairDark;
            else if (name.Equals("Diana", StringComparison.OrdinalIgnoreCase)) hairColor = HairDark;
            else if (name.Equals("Edward", StringComparison.OrdinalIgnoreCase)) hairColor = HairBlonde;
            else if (name.Equals("Fiona", StringComparison.OrdinalIgnoreCase)) hairColor = HairRaven;
        }

        // Headgear color & accent
        NpcColor headgearColor;
        NpcColor headgearAccent;
        switch (headgear)
        {
            case NpcHeadgearType.StrawHat:
                headgearColor = StrawHatBase;
                headgearAccent = StrawHatRibbon;
                break;
            case NpcHeadgearType.MinerHelmet:
                headgearColor = MinerHelmetBase;
                headgearAccent = MinerLampLight;
                break;
            case NpcHeadgearType.Cap:
                headgearColor = CapBase;
                headgearAccent = CapVisor;
                break;
            case NpcHeadgearType.Headband:
                headgearColor = shirtColor; // Harmonizes with their signature color
                headgearAccent = new NpcColor(0.85f, 0.85f, 0.90f); // Silver buckle
                break;
            case NpcHeadgearType.HairOnly:
            default:
                headgearColor = hairColor;
                headgearAccent = hairColor;
                break;
        }

        // Boots and pants styling
        NpcColor bootsColor = archetype switch
        {
            NpcArchetype.Guard => GuardBoots,
            NpcArchetype.Miner => MinerBoots,
            _ => DefaultBoots
        };

        NpcColor pantsColor = archetype switch
        {
            NpcArchetype.Farmer => BrownTrousers,
            NpcArchetype.Woodcutter => BrownTrousers,
            _ => DefaultPants
        };

        // Sleeve cuffs: slightly shaded or linen tone
        NpcColor cuffsColor = new(
            MathF.Min(1.0f, shirtColor.R * 0.85f + 0.15f),
            MathF.Min(1.0f, shirtColor.G * 0.85f + 0.15f),
            MathF.Min(1.0f, shirtColor.B * 0.85f + 0.15f));

        return new NpcAppearanceDetails(
            Archetype: archetype,
            Headgear: headgear,
            HairColor: hairColor,
            HeadgearColor: headgearColor,
            HeadgearAccentColor: headgearAccent,
            ShirtColor: shirtColor,
            PantsColor: pantsColor,
            BootsColor: bootsColor,
            CuffsColor: cuffsColor);
    }
}
