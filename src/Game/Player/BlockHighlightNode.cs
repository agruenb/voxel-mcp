namespace VoxelKing.Game.Player;

using Godot;

/// <summary>
/// Renders a 3D wireframe bounding box highlighting the currently targeted voxel block.
/// </summary>
public partial class BlockHighlightNode : Node3D
{
    private MeshInstance3D _meshInstance = null!;
    private StandardMaterial3D _material = null!;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.1f, 0.1f, 0.1f, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = false,
        };

        _meshInstance = new MeshInstance3D
        {
            Mesh = BuildWireframeCube(1.004f),
            MaterialOverride = _material
        };

        AddChild(_meshInstance);
        Visible = false;
    }

    /// <summary>
    /// Updates position and visual state of the block highlight.
    /// </summary>
    public void UpdateHighlight(bool isHit, Vector3 blockPos, float breakProgress = 0f)
    {
        if (!isHit)
        {
            Visible = false;
            return;
        }

        Visible = true;
        // Position at center of targeted voxel block
        GlobalPosition = new Vector3(
            Mathf.Floor(blockPos.X) + 0.5f,
            Mathf.Floor(blockPos.Y) + 0.5f,
            Mathf.Floor(blockPos.Z) + 0.5f
        );

        // Progress color tint: transitions from dark gray to bright yellow/red as break progress increases
        if (breakProgress > 0.01f)
        {
            float p = Mathf.Clamp(breakProgress, 0f, 1f);
            _material.AlbedoColor = new Color(1f, 1f - (p * 0.7f), 0.1f, 0.95f);
        }
        else
        {
            _material.AlbedoColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        }
    }

    private static ArrayMesh BuildWireframeCube(float size)
    {
        float h = size * 0.5f;

        // 8 cube corners
        Vector3 c0 = new(-h, -h, -h);
        Vector3 c1 = new( h, -h, -h);
        Vector3 c2 = new( h, -h,  h);
        Vector3 c3 = new(-h, -h,  h);
        Vector3 c4 = new(-h,  h, -h);
        Vector3 c5 = new( h,  h, -h);
        Vector3 c6 = new( h,  h,  h);
        Vector3 c7 = new(-h,  h,  h);

        // 12 edges (24 vertices for Line primitives)
        Vector3[] lines =
        [
            // Bottom 4 edges
            c0, c1,  c1, c2,  c2, c3,  c3, c0,
            // Top 4 edges
            c4, c5,  c5, c6,  c6, c7,  c7, c4,
            // Vertical 4 edges
            c0, c4,  c1, c5,  c2, c6,  c3, c7
        ];

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = lines;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        return mesh;
    }
}
