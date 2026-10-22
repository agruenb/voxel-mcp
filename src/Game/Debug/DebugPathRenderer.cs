namespace VoxelKing.Game.Debug;

using System;
using Godot;
using VoxelKing.Core.Npc;

/// <summary>
/// Renders 3D debug lines visualizing active navigation paths for NPCs in the world.
/// </summary>
public partial class DebugPathRenderer : Node3D
{
    private NpcManager? _npcManager;
    private MeshInstance3D _meshInstance = null!;
    private ImmediateMesh _immediateMesh = null!;
    private StandardMaterial3D _lineMaterial = null!;
    private StandardMaterial3D _markerMaterial = null!;

    public bool IsEnabled { get; set; } = true;

    public override void _Ready()
    {
        TopLevel = true;

        _lineMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.2f, 1.0f, 0.4f, 0.95f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = false,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _markerMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 0.85f, 0.1f, 0.95f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = false,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _immediateMesh = new ImmediateMesh();
        _meshInstance = new MeshInstance3D
        {
            Mesh = _immediateMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        AddChild(_meshInstance);
    }

    public void Initialize(NpcManager npcManager)
    {
        _npcManager = npcManager ?? throw new ArgumentNullException(nameof(npcManager));
    }

    public override void _Process(double delta)
    {
        if (!IsEnabled || _npcManager == null || _immediateMesh == null)
        {
            _immediateMesh?.ClearSurfaces();
            return;
        }

        bool hasAnyPath = false;
        foreach (var npc in _npcManager.ActiveNpcs)
        {
            if (npc.ActivePath != null && npc.ActivePath.Count > 0)
            {
                hasAnyPath = true;
                break;
            }
        }

        if (!hasAnyPath)
        {
            _immediateMesh.ClearSurfaces();
            return;
        }

        _immediateMesh.ClearSurfaces();
        _immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _lineMaterial);

        foreach (var npc in _npcManager.ActiveNpcs)
        {
            var path = npc.ActivePath;
            if (path == null || path.Count == 0)
            {
                continue;
            }

            // Start line from NPC's feet (elevated slightly above voxel floor to prevent z-fighting)
            var startPos = new Vector3(npc.Position.X, npc.Position.Y + 0.12f, npc.Position.Z);
            var firstWp = new Vector3(path[0].X + 0.5f, path[0].Y + 0.12f, path[0].Z + 0.5f);

            _immediateMesh.SurfaceAddVertex(startPos);
            _immediateMesh.SurfaceAddVertex(firstWp);

            // Connect consecutive waypoints
            for (int i = 0; i < path.Count - 1; i++)
            {
                var wpA = new Vector3(path[i].X + 0.5f, path[i].Y + 0.12f, path[i].Z + 0.5f);
                var wpB = new Vector3(path[i + 1].X + 0.5f, path[i + 1].Y + 0.12f, path[i + 1].Z + 0.5f);

                _immediateMesh.SurfaceAddVertex(wpA);
                _immediateMesh.SurfaceAddVertex(wpB);
            }
        }

        _immediateMesh.SurfaceEnd();

        // Render destination goal markers
        _immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _markerMaterial);

        foreach (var npc in _npcManager.ActiveNpcs)
        {
            var path = npc.ActivePath;
            if (path == null || path.Count == 0)
            {
                continue;
            }

            var dest = path[^1];
            float cx = dest.X + 0.5f;
            float cy = dest.Y + 0.15f;
            float cz = dest.Z + 0.5f;
            float size = 0.35f;

            // Draw an 'X' marker on the destination block
            _immediateMesh.SurfaceAddVertex(new Vector3(cx - size, cy, cz - size));
            _immediateMesh.SurfaceAddVertex(new Vector3(cx + size, cy, cz + size));

            _immediateMesh.SurfaceAddVertex(new Vector3(cx - size, cy, cz + size));
            _immediateMesh.SurfaceAddVertex(new Vector3(cx + size, cy, cz - size));
        }

        _immediateMesh.SurfaceEnd();
    }
}
