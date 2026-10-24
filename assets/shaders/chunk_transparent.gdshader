shader_type spatial;
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
    // 1. Determine local 2D face coordinates from model-space position and normal
    vec2 local_uv;
    if (abs(v_normal.y) > 0.5) {
        // Top (+Y) and Bottom (-Y)
        local_uv = vec2(v_pos.x, v_pos.z);
    } else if (abs(v_normal.x) > 0.5) {
        // East (+X) and West (-X)
        local_uv = vec2(v_pos.z, 1.0 - v_pos.y);
    } else {
        // South (+Z) and North (-Z)
        local_uv = vec2(v_pos.x, 1.0 - v_pos.y);
    }

    // 2. Identify the atlas tile from the quad UV
    vec2 tile_col_row = clamp(floor(UV * atlas_size), vec2(0.0), atlas_size - vec2(1.0));
    vec2 tile_uv_size = vec2(1.0) / atlas_size;
    vec2 tile_uv_min = tile_col_row * tile_uv_size;

    // 3. Wrap block_uv to [0, 1] per 1m voxel block, clamping edges to prevent atlas bleeding
    vec2 block_uv = fract(local_uv);
    block_uv = clamp(block_uv, vec2(0.001), vec2(0.999));
    vec2 atlas_uv = tile_uv_min + block_uv * tile_uv_size;

    // 4. Sample with continuous derivatives to prevent seams across block boundaries
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

