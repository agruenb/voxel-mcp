namespace VoxelKing.Core.Npc;

using System;
using System.Globalization;

/// <summary>
/// Immutable RGB color representation for NPC appearance and clothing customization in Core simulation.
/// Avoids engine-specific dependencies (e.g. Godot.Color).
/// </summary>
public readonly record struct NpcColor(float R, float G, float B, float A = 1.0f)
{
    public static readonly NpcColor DefaultShirt = new(0.24f, 0.48f, 0.82f); // Default blue shirt

    /// <summary>
    /// Attempts to parse an HTML hex code (#RGB, #RGBA, #RRGGBB, #RRGGBBAA) or standard named color.
    /// </summary>
    public static bool TryParse(string? text, out NpcColor color)
    {
        color = DefaultShirt;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();

        if (TryGetNamedColor(trimmed, out color))
        {
            return true;
        }

        if (trimmed.StartsWith('#'))
        {
            string hex = trimmed[1..];
            if (hex.Length == 3) // #RGB
            {
                if (int.TryParse(hex[0].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) &&
                    int.TryParse(hex[1].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) &&
                    int.TryParse(hex[2].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                {
                    color = new NpcColor((r * 17) / 255f, (g * 17) / 255f, (b * 17) / 255f, 1f);
                    return true;
                }
            }
            else if (hex.Length == 4) // #RGBA
            {
                if (int.TryParse(hex[0].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) &&
                    int.TryParse(hex[1].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) &&
                    int.TryParse(hex[2].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b) &&
                    int.TryParse(hex[3].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int a))
                {
                    color = new NpcColor((r * 17) / 255f, (g * 17) / 255f, (b * 17) / 255f, (a * 17) / 255f);
                    return true;
                }
            }
            else if (hex.Length == 6) // #RRGGBB
            {
                if (int.TryParse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) &&
                    int.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) &&
                    int.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                {
                    color = new NpcColor(r / 255f, g / 255f, b / 255f, 1f);
                    return true;
                }
            }
            else if (hex.Length == 8) // #RRGGBBAA
            {
                if (int.TryParse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) &&
                    int.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) &&
                    int.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b) &&
                    int.TryParse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int a))
                {
                    color = new NpcColor(r / 255f, g / 255f, b / 255f, a / 255f);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Formats this color as a standard HTML hexadecimal color code (e.g. "#3D7BEB").
    /// </summary>
    public string ToHex(bool includeAlpha = false)
    {
        int r = Math.Clamp((int)MathF.Round(R * 255f), 0, 255);
        int g = Math.Clamp((int)MathF.Round(G * 255f), 0, 255);
        int b = Math.Clamp((int)MathF.Round(B * 255f), 0, 255);
        if (includeAlpha)
        {
            int a = Math.Clamp((int)MathF.Round(A * 255f), 0, 255);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static bool TryGetNamedColor(string name, out NpcColor color)
    {
        switch (name.ToLowerInvariant())
        {
            case "red": color = new NpcColor(0.9f, 0.2f, 0.2f); return true;
            case "green": color = new NpcColor(0.2f, 0.8f, 0.3f); return true;
            case "blue": color = new NpcColor(0.2f, 0.45f, 0.9f); return true;
            case "yellow": color = new NpcColor(0.95f, 0.85f, 0.2f); return true;
            case "orange": color = new NpcColor(0.95f, 0.55f, 0.15f); return true;
            case "purple": color = new NpcColor(0.6f, 0.3f, 0.8f); return true;
            case "cyan": case "teal": color = new NpcColor(0.15f, 0.75f, 0.75f); return true;
            case "white": color = new NpcColor(1f, 1f, 1f); return true;
            case "black": color = new NpcColor(0.1f, 0.1f, 0.1f); return true;
            case "gray": case "grey": color = new NpcColor(0.5f, 0.5f, 0.5f); return true;
            default:
                color = DefaultShirt;
                return false;
        }
    }
}
