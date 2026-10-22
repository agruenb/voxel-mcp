namespace VoxelKing.Game.Npc;

using System;
using Godot;
using VoxelKing.Core.Npc;

/// <summary>
/// Visual representation of an NPC in the Godot scene tree.
/// Renders a low-poly blocky humanoid with name tag, walk/idle animation, and interpolates from NpcState.
/// Contains no simulation logic.
/// </summary>
public partial class NpcNode : Node3D
{
    public NpcState? State { get; private set; }

    public Node3D Visuals { get; private set; } = null!;
    public MeshInstance3D HeadMesh { get; private set; } = null!;
    public MeshInstance3D TorsoMesh { get; private set; } = null!;
    public Node3D LeftArmPivot { get; private set; } = null!;
    public MeshInstance3D LeftArmMesh { get; private set; } = null!;
    public Node3D RightArmPivot { get; private set; } = null!;
    public MeshInstance3D RightArmMesh { get; private set; } = null!;
    public Node3D LeftLegPivot { get; private set; } = null!;
    public MeshInstance3D LeftLegMesh { get; private set; } = null!;
    public Node3D RightLegPivot { get; private set; } = null!;
    public MeshInstance3D RightLegMesh { get; private set; } = null!;
    public Label3D NameTag { get; private set; } = null!;
    public SpeechBubble3D SpeechBubble { get; private set; } = null!;

    private double _walkTime;
    private double _idleTime;
    private bool _isBuilt;

    public override void _Ready()
    {
        EnsureVisualHierarchy();
        if (State != null && NameTag != null)
        {
            NameTag.Text = State.Name;
        }
    }

    /// <summary>
    /// Binds this visual node to an authoritative NpcState in Core simulation.
    /// </summary>
    public void BindState(NpcState state)
    {
        State = state;
        GlobalPosition = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
        Rotation = new Vector3(0, state.Facing, 0);
        EnsureVisualHierarchy();
        if (NameTag != null)
        {
            NameTag.Text = state.Name;
        }
    }

    public override void _Process(double delta)
    {
        if (State == null)
        {
            return;
        }

        Vector3 targetPos = new(State.Position.X, State.Position.Y, State.Position.Z);
        float dist = GlobalPosition.DistanceTo(targetPos);

        // Position interpolation
        if (dist > 4.0f)
        {
            GlobalPosition = targetPos;
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(targetPos, (float)(20.0 * delta));
        }

        // Yaw rotation interpolation
        float currentYaw = Rotation.Y;
        float targetYaw = State.Facing;
        Rotation = new Vector3(0, Mathf.LerpAngle(currentYaw, targetYaw, (float)(15.0 * delta)), 0);

        // Synchronize name tag
        if (NameTag != null && NameTag.Text != State.Name)
        {
            NameTag.Text = State.Name;
        }

        // Walk and Idle limb animations
        float horizontalSpeed = new Vector2(State.Velocity.X, State.Velocity.Z).Length();

        if (horizontalSpeed > 0.1f && State.IsOnGround)
        {
            _walkTime += delta * horizontalSpeed * 4.5;
            float legAngle = Mathf.Sin((float)_walkTime) * 0.6f;
            float armAngle = -legAngle * 0.7f;

            LeftLegPivot.Rotation = new Vector3(legAngle, 0, 0);
            RightLegPivot.Rotation = new Vector3(-legAngle, 0, 0);
            LeftArmPivot.Rotation = new Vector3(armAngle, 0, 0);
            RightArmPivot.Rotation = new Vector3(-armAngle, 0, 0);
            Visuals.Position = new Vector3(0, Mathf.Abs(Mathf.Sin((float)_walkTime * 2.0f)) * 0.03f, 0);
        }
        else
        {
            float damp = (float)(10.0 * delta);
            LeftLegPivot.Rotation = LeftLegPivot.Rotation.Lerp(Vector3.Zero, damp);
            RightLegPivot.Rotation = RightLegPivot.Rotation.Lerp(Vector3.Zero, damp);
            LeftArmPivot.Rotation = LeftArmPivot.Rotation.Lerp(Vector3.Zero, damp);
            RightArmPivot.Rotation = RightArmPivot.Rotation.Lerp(Vector3.Zero, damp);

            _idleTime += delta;
            Visuals.Position = new Vector3(0, Mathf.Sin((float)_idleTime * 2.0f) * 0.012f, 0);
        }
    }

    private void EnsureVisualHierarchy()
    {
        if (_isBuilt)
        {
            return;
        }

        // Try getting nodes from scene if present
        Visuals = GetNodeOrNull<Node3D>("Visuals")!;
        if (Visuals == null)
        {
            Visuals = new Node3D { Name = "Visuals" };
            AddChild(Visuals);

            var skinMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.76f, 0.65f) };
            var shirtMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.48f, 0.82f) };
            var pantsMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.22f, 0.32f) };

            // Torso (0.45 x 0.6 x 0.25) at Y = 1.05
            TorsoMesh = new MeshInstance3D
            {
                Name = "Torso",
                Mesh = new BoxMesh { Size = new Vector3(0.45f, 0.6f, 0.25f) },
                Position = new Vector3(0, 1.05f, 0),
                MaterialOverride = shirtMaterial
            };
            Visuals.AddChild(TorsoMesh);

            // Head (0.4 x 0.4 x 0.4) at Y = 1.55
            HeadMesh = new MeshInstance3D
            {
                Name = "Head",
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.4f, 0.4f) },
                Position = new Vector3(0, 1.55f, 0),
                MaterialOverride = skinMaterial
            };
            Visuals.AddChild(HeadMesh);

            // Left Arm Pivot at (-0.31, 1.3, 0)
            LeftArmPivot = new Node3D { Name = "LeftArmPivot", Position = new Vector3(-0.31f, 1.3f, 0) };
            LeftArmMesh = new MeshInstance3D
            {
                Name = "LeftArm",
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.55f, 0.16f) },
                Position = new Vector3(0, -0.25f, 0),
                MaterialOverride = skinMaterial
            };
            LeftArmPivot.AddChild(LeftArmMesh);
            Visuals.AddChild(LeftArmPivot);

            // Right Arm Pivot at (0.31, 1.3, 0)
            RightArmPivot = new Node3D { Name = "RightArmPivot", Position = new Vector3(0.31f, 1.3f, 0) };
            RightArmMesh = new MeshInstance3D
            {
                Name = "RightArm",
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.55f, 0.16f) },
                Position = new Vector3(0, -0.25f, 0),
                MaterialOverride = skinMaterial
            };
            RightArmPivot.AddChild(RightArmMesh);
            Visuals.AddChild(RightArmPivot);

            // Left Leg Pivot at (-0.12, 0.75, 0)
            LeftLegPivot = new Node3D { Name = "LeftLegPivot", Position = new Vector3(-0.12f, 0.75f, 0) };
            LeftLegMesh = new MeshInstance3D
            {
                Name = "LeftLeg",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.75f, 0.18f) },
                Position = new Vector3(0, -0.375f, 0),
                MaterialOverride = pantsMaterial
            };
            LeftLegPivot.AddChild(LeftLegMesh);
            Visuals.AddChild(LeftLegPivot);

            // Right Leg Pivot at (0.12, 0.75, 0)
            RightLegPivot = new Node3D { Name = "RightLegPivot", Position = new Vector3(0.12f, 0.75f, 0) };
            RightLegMesh = new MeshInstance3D
            {
                Name = "RightLeg",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.75f, 0.18f) },
                Position = new Vector3(0, -0.375f, 0),
                MaterialOverride = pantsMaterial
            };
            RightLegPivot.AddChild(RightLegMesh);
            Visuals.AddChild(RightLegPivot);

            // Floating NameTag at Y = 1.95
            NameTag = new Label3D
            {
                Name = "NameTag",
                Position = new Vector3(0, 1.95f, 0),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                OutlineSize = 8,
                FontSize = 24,
                Text = State?.Name ?? "NPC"
            };
            AddChild(NameTag);

            // Floating Speech Bubble at Y = 2.30
            SpeechBubble = new SpeechBubble3D
            {
                Name = "SpeechBubble",
                Position = new Vector3(0, 2.30f, 0)
            };
            AddChild(SpeechBubble);
        }
        else
        {
            TorsoMesh = Visuals.GetNode<MeshInstance3D>("Torso");
            HeadMesh = Visuals.GetNode<MeshInstance3D>("Head");
            LeftArmPivot = Visuals.GetNode<Node3D>("LeftArmPivot");
            LeftArmMesh = LeftArmPivot.GetNode<MeshInstance3D>("LeftArm");
            RightArmPivot = Visuals.GetNode<Node3D>("RightArmPivot");
            RightArmMesh = RightArmPivot.GetNode<MeshInstance3D>("RightArm");
            LeftLegPivot = Visuals.GetNode<Node3D>("LeftLegPivot");
            LeftLegMesh = LeftLegPivot.GetNode<MeshInstance3D>("LeftLeg");
            RightLegPivot = Visuals.GetNode<Node3D>("RightLegPivot");
            RightLegMesh = RightLegPivot.GetNode<MeshInstance3D>("RightLeg");
            NameTag = GetNode<Label3D>("NameTag");
            SpeechBubble = GetNodeOrNull<SpeechBubble3D>("SpeechBubble")!;
            if (SpeechBubble == null)
            {
                SpeechBubble = new SpeechBubble3D { Name = "SpeechBubble", Position = new Vector3(0, 2.30f, 0) };
                AddChild(SpeechBubble);
            }
        }

        _isBuilt = true;
    }

    /// <summary>
    /// Displays a speech message in the speech bubble floating above the NPC.
    /// </summary>
    public void ShowSpeechBubble(string text, float duration = SpeechBubble3D.DefaultDuration)
    {
        EnsureVisualHierarchy();
        SpeechBubble?.ShowSpeech(text, duration);
    }
}
