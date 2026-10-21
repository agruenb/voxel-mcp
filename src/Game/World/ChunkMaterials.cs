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
        var texture = GD.Load<Texture2D>(atlasPath);
        if (texture == null)
        {
            throw new InvalidOperationException($"Failed to load block texture atlas from '{atlasPath}'.");
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
