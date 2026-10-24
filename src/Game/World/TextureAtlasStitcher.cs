namespace VoxelKing.Game.World;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using VoxelKing.Core.Voxel;

/// <summary>
/// Result of an atlas stitching operation containing the stitched Godot texture, layout config, and name mappings.
/// </summary>
public sealed record TextureAtlasStitchResult(
    Texture2D AtlasTexture,
    TextureAtlasConfig Config,
    IReadOnlyDictionary<string, int> TextureIndices
);

/// <summary>
/// Dynamically stitches individual block texture PNGs from assets/textures/blocks/ into a single runtime atlas.
/// Eliminates the need to manually edit composite sprite sheets.
/// </summary>
public static class TextureAtlasStitcher
{
    private static readonly string[] StandardTextureOrder =
    [
        "grass_top",
        "grass_side",
        "dirt",
        "stone",
        "sand",
        "water",
        "oak_log_top",
        "oak_log_side",
        "oak_leaves",
        "planks",
        "cobblestone",
        "chest_top",
        "chest_side",
        "coal_ore"
    ];

    private static TextureAtlasStitchResult? _cachedResult;

    /// <summary>
    /// Gets the existing stitched atlas or performs stitching if not yet loaded.
    /// </summary>
    public static TextureAtlasStitchResult GetOrStitch(bool forceReload = false)
    {
        if (!forceReload && _cachedResult != null)
        {
            return _cachedResult;
        }

        _cachedResult = Stitch();
        return _cachedResult;
    }

    /// <summary>
    /// Scans the block textures folder, builds a stitched composite atlas, and updates the cached texture.
    /// </summary>
    public static TextureAtlasStitchResult Stitch(
        string blocksResDir = "res://assets/textures/blocks",
        string atlasResPath = "res://assets/textures/blocks_atlas.png")
    {
        string globalBlocksDir = ProjectSettings.GlobalizePath(blocksResDir);
        string globalAtlasPath = ProjectSettings.GlobalizePath(atlasResPath);

        if (!Directory.Exists(globalBlocksDir))
        {
            return FallbackToStaticAtlas(globalAtlasPath);
        }

        var pngFiles = Directory.GetFiles(globalBlocksDir, "*.png")
            .Where(f => !f.EndsWith(".import", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pngFiles.Count == 0)
        {
            return FallbackToStaticAtlas(globalAtlasPath);
        }

        // Map files by texture name (without extension)
        var fileByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in pngFiles)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            fileByName[name] = file;
        }

        // Order: Standard textures first (for backwards compatibility with integer atlas indices),
        // followed by any new/custom textures alphabetically.
        var orderedNames = new List<string>();
        foreach (var standard in StandardTextureOrder)
        {
            if (fileByName.ContainsKey(standard))
            {
                orderedNames.Add(standard);
            }
        }

        foreach (var name in fileByName.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            if (!orderedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                orderedNames.Add(name);
            }
        }

        // Determine grid dimension: minimum 4x4 (16 tiles), expanding by powers of 2 (8x8, 16x16) as needed
        int count = orderedNames.Count;
        int columns = 4;
        while (columns * columns < count)
        {
            columns *= 2;
        }
        int rows = columns;

        // Inspect first tile to detect tile resolution (typically 32x32)
        int tileSize = 32;
        var firstImg = Image.LoadFromFile(fileByName[orderedNames[0]]);
        if (firstImg != null && firstImg.GetWidth() > 0)
        {
            tileSize = firstImg.GetWidth();
        }

        int atlasWidth = columns * tileSize;
        int atlasHeight = rows * tileSize;

        var atlasImage = Image.CreateEmpty(atlasWidth, atlasHeight, false, Image.Format.Rgba8);
        atlasImage.Fill(new Color(0f, 0f, 0f, 1f));

        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < orderedNames.Count; i++)
        {
            string texName = orderedNames[i];
            indices[texName] = i;

            int col = i % columns;
            int row = i / columns;
            int destX = col * tileSize;
            int destY = row * tileSize;

            string filePath = fileByName[texName];
            var tileImg = Image.LoadFromFile(filePath);
            if (tileImg != null)
            {
                if (tileImg.GetFormat() != Image.Format.Rgba8)
                {
                    tileImg.Convert(Image.Format.Rgba8);
                }
                atlasImage.BlitRect(tileImg, new Rect2I(0, 0, tileSize, tileSize), new Vector2I(destX, destY));
            }
        }

        // Save composite atlas to disk to keep cached texture asset synchronized
        try
        {
            atlasImage.SavePng(globalAtlasPath);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to save stitched atlas cache to '{globalAtlasPath}': {ex.Message}");
        }

        var imageTexture = ImageTexture.CreateFromImage(atlasImage);
        var config = new TextureAtlasConfig(columns, rows);

        return new TextureAtlasStitchResult(imageTexture, config, indices);
    }

    private static TextureAtlasStitchResult FallbackToStaticAtlas(string globalAtlasPath)
    {
        Texture2D texture;
        if (File.Exists(globalAtlasPath))
        {
            var img = Image.LoadFromFile(globalAtlasPath);
            texture = ImageTexture.CreateFromImage(img);
        }
        else
        {
            var img = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
            img.Fill(new Color(0.5f, 0.5f, 0.5f, 1f));
            texture = ImageTexture.CreateFromImage(img);
        }

        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < StandardTextureOrder.Length; i++)
        {
            indices[StandardTextureOrder[i]] = i;
        }

        return new TextureAtlasStitchResult(texture, TextureAtlasConfig.Default, indices);
    }
}
