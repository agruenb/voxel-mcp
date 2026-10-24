namespace VoxelKing.Game.World;

using System;
using Godot;
using VoxelKing.Core.Voxel;

/// <summary>
/// Container and factory for shared chunk rendering materials.
/// Minimizes draw calls and shader state changes across all chunk nodes.
/// </summary>
public sealed class ChunkMaterials
{
    public Material Opaque { get; }
    public Material Transparent { get; }
    public Texture2D AtlasTexture { get; }
    public TextureAtlasConfig AtlasConfig { get; }

    public ChunkMaterials()
    {
        var stitchResult = TextureAtlasStitcher.GetOrStitch();
        AtlasTexture = stitchResult.AtlasTexture;
        AtlasConfig = stitchResult.Config;

        var opaqueShader = LoadShader("res://assets/shaders/chunk_opaque.gdshader", OpaqueShaderFallback);
        var opaqueMat = new ShaderMaterial
        {
            Shader = opaqueShader
        };
        opaqueMat.SetShaderParameter("texture_albedo", AtlasTexture);
        opaqueMat.SetShaderParameter("atlas_size", new Vector2(AtlasConfig.Columns, AtlasConfig.Rows));
        Opaque = opaqueMat;

        var transparentShader = LoadShader("res://assets/shaders/chunk_transparent.gdshader", TransparentShaderFallback);
        var transparentMat = new ShaderMaterial
        {
            Shader = transparentShader
        };
        transparentMat.SetShaderParameter("texture_albedo", AtlasTexture);
        transparentMat.SetShaderParameter("atlas_size", new Vector2(AtlasConfig.Columns, AtlasConfig.Rows));
        Transparent = transparentMat;
    }

    public ChunkMaterials(Texture2D atlasTexture, TextureAtlasConfig atlasConfig)
    {
        ArgumentNullException.ThrowIfNull(atlasTexture);
        ArgumentNullException.ThrowIfNull(atlasConfig);

        AtlasTexture = atlasTexture;
        AtlasConfig = atlasConfig;

        var opaqueShader = LoadShader("res://assets/shaders/chunk_opaque.gdshader", OpaqueShaderFallback);
        var opaqueMat = new ShaderMaterial
        {
            Shader = opaqueShader
        };
        opaqueMat.SetShaderParameter("texture_albedo", AtlasTexture);
        opaqueMat.SetShaderParameter("atlas_size", new Vector2(AtlasConfig.Columns, AtlasConfig.Rows));
        Opaque = opaqueMat;

        var transparentShader = LoadShader("res://assets/shaders/chunk_transparent.gdshader", TransparentShaderFallback);
        var transparentMat = new ShaderMaterial
        {
            Shader = transparentShader
        };
        transparentMat.SetShaderParameter("texture_albedo", AtlasTexture);
        transparentMat.SetShaderParameter("atlas_size", new Vector2(AtlasConfig.Columns, AtlasConfig.Rows));
        Transparent = transparentMat;
    }

    public ChunkMaterials(string atlasPath)
        : this()
    {
    }

    private static Shader LoadShader(string resourcePath, string fallbackCode)
    {
        try
        {
            if (ResourceLoader.Exists(resourcePath))
            {
                var shader = GD.Load<Shader>(resourcePath);
                if (shader != null) return shader;
            }
        }
        catch
        {
            // Fall back to direct file read
        }

        try
        {
            string globalPath = ProjectSettings.GlobalizePath(resourcePath);
            if (System.IO.File.Exists(globalPath))
            {
                string code = System.IO.File.ReadAllText(globalPath);
                return new Shader { Code = code };
            }
        }
        catch
        {
            // Fall back to embedded code
        }

        return new Shader { Code = fallbackCode };
    }

    private const string OpaqueShaderFallback = @"shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

uniform sampler2D texture_albedo : source_color, filter_nearest;
uniform vec2 atlas_size = vec2(4.0, 4.0);

varying vec3 v_pos;
varying vec3 v_normal;

void vertex() {
    v_pos = VERTEX;
    v_normal = NORMAL;
}

void fragment() {
    vec2 local_uv;
    if (abs(v_normal.y) > 0.5) {
        local_uv = vec2(v_pos.x, v_pos.z);
    } else if (abs(v_normal.x) > 0.5) {
        local_uv = vec2(v_pos.z, 1.0 - v_pos.y);
    } else {
        local_uv = vec2(v_pos.x, 1.0 - v_pos.y);
    }

    vec2 tile_col_row = clamp(floor(UV * atlas_size), vec2(0.0), atlas_size - vec2(1.0));
    vec2 tile_uv_size = vec2(1.0) / atlas_size;
    vec2 tile_uv_min = tile_col_row * tile_uv_size;

    vec2 block_uv = fract(local_uv);
    block_uv = clamp(block_uv, vec2(0.001), vec2(0.999));
    vec2 atlas_uv = tile_uv_min + block_uv * tile_uv_size;

    vec2 dX = dFdx(local_uv) * tile_uv_size;
    vec2 dY = dFdy(local_uv) * tile_uv_size;
    vec4 tex_color = textureGrad(texture_albedo, atlas_uv, dX, dY);

    ALBEDO = tex_color.rgb;
    ROUGHNESS = 1.0;
}
";

    private const string TransparentShaderFallback = @"shader_type spatial;
render_mode cull_back, depth_draw_always, diffuse_lambert, specular_schlick_ggx;

uniform sampler2D texture_albedo : source_color, filter_nearest;
uniform vec2 atlas_size = vec2(4.0, 4.0);

varying vec3 v_pos;
varying vec3 v_normal;

void vertex() {
    v_pos = VERTEX;
    v_normal = NORMAL;
}

void fragment() {
    vec2 local_uv;
    if (abs(v_normal.y) > 0.5) {
        local_uv = vec2(v_pos.x, v_pos.z);
    } else if (abs(v_normal.x) > 0.5) {
        local_uv = vec2(v_pos.z, 1.0 - v_pos.y);
    } else {
        local_uv = vec2(v_pos.x, 1.0 - v_pos.y);
    }

    vec2 tile_col_row = clamp(floor(UV * atlas_size), vec2(0.0), atlas_size - vec2(1.0));
    vec2 tile_uv_size = vec2(1.0) / atlas_size;
    vec2 tile_uv_min = tile_col_row * tile_uv_size;

    vec2 block_uv = fract(local_uv);
    block_uv = clamp(block_uv, vec2(0.001), vec2(0.999));
    vec2 atlas_uv = tile_uv_min + block_uv * tile_uv_size;

    vec2 dX = dFdx(local_uv) * tile_uv_size;
    vec2 dY = dFdy(local_uv) * tile_uv_size;
    vec4 tex_color = textureGrad(texture_albedo, atlas_uv, dX, dY);

    if (tex_color.a < 0.1) {
        discard;
    }

    ALBEDO = tex_color.rgb;
    ALPHA = tex_color.a;
    ROUGHNESS = 0.8;
}
";
}
