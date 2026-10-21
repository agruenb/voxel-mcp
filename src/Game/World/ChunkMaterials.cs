namespace VoxelKing.Game.World;

using System;
using Godot;

/// <summary>
/// Container and factory for shared chunk rendering materials.
/// Minimizes draw calls and shader state changes across all chunk nodes.
/// </summary>
public sealed class ChunkMaterials
{
    public StandardMaterial3D Opaque { get; }
    public StandardMaterial3D Transparent { get; }
    public Texture2D AtlasTexture { get; }

    public ChunkMaterials(string atlasPath = "res://assets/textures/blocks_atlas.png")
    {
        Texture2D? texture = null;
        try
        {
            if (ResourceLoader.Exists(atlasPath))
            {
                texture = GD.Load<Texture2D>(atlasPath);
            }
        }
        catch
        {
            // Fall back to direct file loading
        }

        if (texture == null)
        {
            string globalPath = ProjectSettings.GlobalizePath(atlasPath);
            if (System.IO.File.Exists(globalPath))
            {
                var image = Image.LoadFromFile(globalPath);
                texture = ImageTexture.CreateFromImage(image);
            }
            else
            {
                // Fallback 128x128 blank texture if file does not exist
                var image = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
                image.Fill(new Color(0.5f, 0.5f, 0.5f, 1f));
                texture = ImageTexture.CreateFromImage(image);
            }
        }

        AtlasTexture = texture;

        Opaque = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 1.0f,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
        };

        Transparent = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.5f,
        };
    }
}
