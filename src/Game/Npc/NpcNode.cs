namespace VoxelKing.Game.Npc;

using System;
using Godot;
using VoxelKing.Core.Common;
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

    // Facial hierarchy (F-210 Phase 1)
    public Node3D FaceRoot { get; private set; } = null!;
    public Node3D LeftEyePivot { get; private set; } = null!;
    public Node3D RightEyePivot { get; private set; } = null!;
    public MeshInstance3D LeftEyeMesh { get; private set; } = null!;
    public MeshInstance3D RightEyeMesh { get; private set; } = null!;
    public MeshInstance3D LeftPupilMesh { get; private set; } = null!;
    public MeshInstance3D RightPupilMesh { get; private set; } = null!;
    public Node3D LeftEyebrowPivot { get; private set; } = null!;
    public Node3D RightEyebrowPivot { get; private set; } = null!;
    public MeshInstance3D LeftEyebrowMesh { get; private set; } = null!;
    public MeshInstance3D RightEyebrowMesh { get; private set; } = null!;
    public Node3D MouthPivot { get; private set; } = null!;
    public MeshInstance3D MouthMesh { get; private set; } = null!;

    // Head-tracking IK & Look-at state
    public Vector3? TargetLookAtPosition { get; set; }
    public bool IsTrackingTarget { get; private set; }
    public float HeadYaw { get; private set; }
    public float HeadPitch { get; private set; }
    public bool IsBlinking => _isBlinking;
    public NpcExpressionState Expression { get; private set; } = NpcExpressionState.Default;

    // Locomotion dynamics (F-210 Phase 2)
    public float TurnBankAngle => _currentTorsoRoll;
    public float ForwardLeanAngle => _currentTorsoPitch;
    public bool IsAirborne => State != null && !State.IsOnGround;
    public bool IsLandingSquash => _isLandingImpact;

    // Hand attachment socket & held items (F-210 Phase 3)
    public Node3D RightHandSlot { get; private set; } = null!;
    public Node3D HeldItemNode { get; private set; } = null!;
    public string? CurrentHeldItemId { get; private set; }
    public bool IsActionSwinging { get; set; }
    public NpcGestureState GestureState { get; private set; } = NpcGestureState.None;

    // Archetype Customization & Silhouettes (F-210 Phase 4)
    public Node3D HairRoot { get; private set; } = null!;
    public Node3D HeadgearRoot { get; private set; } = null!;
    public MeshInstance3D LeftBootMesh { get; private set; } = null!;
    public MeshInstance3D RightBootMesh { get; private set; } = null!;
    public MeshInstance3D LeftCuffMesh { get; private set; } = null!;
    public MeshInstance3D RightCuffMesh { get; private set; } = null!;
    public NpcArchetype Archetype { get; private set; } = NpcArchetype.Villager;
    public NpcHeadgearType HeadgearType { get; private set; } = NpcHeadgearType.HairOnly;
    public NpcAppearanceDetails AppearanceDetails { get; private set; }

    private float _angularVelocity;
    private float _currentTorsoRoll;
    private float _currentTorsoPitch;
    private bool _wasOnGround = true;
    private bool _isLandingImpact;
    private float _landingProgress;

    private double _walkTime;
    private double _idleTime;
    private double _talkTime;
    private double _swingTime;
    private double _scratchTime;
    private double _gesticulateTime;
    private bool _isBuilt;

    // Head rotation interpolation
    private float _currentHeadYaw;
    private float _currentHeadPitch;
    private float _targetHeadYaw;
    private float _targetHeadPitch;

    // Idle glance timer
    private float _idleGlanceTimer;
    private float _idleTargetYaw;
    private float _idleTargetPitch;

    // Blink system
    private float _blinkTimer;
    private bool _isBlinking;
    private float _blinkProgress;
    private float _blinkScale = 1.0f;

    public override void _Ready()
    {
        EnsureVisualHierarchy();
        if (State != null && NameTag != null)
        {
            NameTag.Text = State.Name;
        }
        ApplyAppearance();

        _blinkTimer = (float)GD.RandRange(NpcHeadTracking.MinBlinkInterval, NpcHeadTracking.MaxBlinkInterval);
        _idleGlanceTimer = (float)GD.RandRange(2.0, 4.5);
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
        ApplyAppearance();
    }

    /// <summary>
    /// Applies visual appearance customization (distinct shirt colour, archetype headgear, hair, boots, and cuffs) from NpcState.
    /// </summary>
    public void ApplyAppearance()
    {
        if (State == null)
        {
            var defaultDetails = NpcAppearanceCustomization.ResolveAppearanceDetails(null, "NPC", null, null, null);
            ApplyAppearanceCustomization(defaultDetails);
            return;
        }

        if (NameTag != null)
        {
            NameTag.Text = State.Name;
        }

        var details = NpcAppearanceCustomization.ResolveAppearanceDetails(
            State.Archetype,
            State.Name,
            State.Skills,
            State.Personality,
            State.AppearanceColor);

        ApplyAppearanceCustomization(details);
    }

    /// <summary>
    /// Applies full clothing accents (boots, cuffs, pants, shirt) and archetype headgear.
    /// </summary>
    public void ApplyAppearanceCustomization(NpcAppearanceDetails details)
    {
        AppearanceDetails = details;
        Archetype = details.Archetype;
        HeadgearType = details.Headgear;

        if (TorsoMesh != null)
        {
            TorsoMesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(details.ShirtColor.R, details.ShirtColor.G, details.ShirtColor.B)
            };
        }

        var pantsMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(details.PantsColor.R, details.PantsColor.G, details.PantsColor.B)
        };

        if (LeftLegMesh != null)
        {
            LeftLegMesh.MaterialOverride = pantsMat;
        }
        if (RightLegMesh != null)
        {
            RightLegMesh.MaterialOverride = pantsMat;
        }

        var bootsMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(details.BootsColor.R, details.BootsColor.G, details.BootsColor.B)
        };

        if (LeftBootMesh != null)
        {
            LeftBootMesh.MaterialOverride = bootsMat;
        }
        if (RightBootMesh != null)
        {
            RightBootMesh.MaterialOverride = bootsMat;
        }

        var cuffsMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(details.CuffsColor.R, details.CuffsColor.G, details.CuffsColor.B)
        };

        if (LeftCuffMesh != null)
        {
            LeftCuffMesh.MaterialOverride = cuffsMat;
        }
        if (RightCuffMesh != null)
        {
            RightCuffMesh.MaterialOverride = cuffsMat;
        }

        BuildHairAndHeadgear(details);
    }

    /// <summary>
    /// Manually sets and applies an archetype customization style.
    /// </summary>
    public void SetArchetype(NpcArchetype archetype)
    {
        var details = NpcAppearanceCustomization.ResolveAppearanceDetails(
            archetype.ToString(),
            State?.Name,
            State?.Skills,
            State?.Personality,
            State?.AppearanceColor);

        ApplyAppearanceCustomization(details);
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

        // Yaw rotation interpolation and turning rate calculation
        float currentYaw = Rotation.Y;
        float targetYaw = State.Facing;
        float angleDiff = Mathf.AngleDifference(currentYaw, targetYaw);
        float turningRate = angleDiff / (float)Mathf.Max(0.0001, delta);
        _angularVelocity = Mathf.Lerp(_angularVelocity, turningRate, (float)(10.0 * delta));

        Rotation = new Vector3(0, Mathf.LerpAngle(currentYaw, targetYaw, (float)(15.0 * delta)), 0);

        // Synchronize name tag
        if (NameTag != null && NameTag.Text != State.Name)
        {
            NameTag.Text = State.Name;
        }

        // F-210 Phase 2: Locomotion dynamics (turn banking, forward lean, airborne posing, and landing squash)
        float horizontalSpeed = new Vector2(State.Velocity.X, State.Velocity.Z).Length();
        bool isOnGround = State.IsOnGround;

        // 1. Turn banking & forward lean
        float speedFactor = Mathf.Clamp(horizontalSpeed / 2.0f, 0.2f, 1.0f);
        float targetRoll = NpcLocomotionDynamics.CalculateTurnBank(_angularVelocity) * speedFactor;
        float targetPitch = NpcLocomotionDynamics.CalculateForwardLean(horizontalSpeed);
        _currentTorsoRoll = Mathf.Lerp(_currentTorsoRoll, targetRoll, (float)(8.0 * delta));
        _currentTorsoPitch = Mathf.Lerp(_currentTorsoPitch, targetPitch, (float)(8.0 * delta));
        Visuals.Rotation = new Vector3(_currentTorsoPitch, 0f, _currentTorsoRoll);

        // 2. Landing impact squash & stretch transition detection
        if (!_wasOnGround && isOnGround)
        {
            _isLandingImpact = true;
            _landingProgress = 0f;
        }
        _wasOnGround = isOnGround;

        if (_isLandingImpact)
        {
            _landingProgress += (float)delta;
            if (_landingProgress >= NpcLocomotionDynamics.DefaultLandingDuration)
            {
                _isLandingImpact = false;
                _landingProgress = 0f;
                Visuals.Scale = Vector3.One;
            }
            else
            {
                var squash = NpcLocomotionDynamics.CalculateLandingSquash(_landingProgress);
                Visuals.Scale = new Vector3(squash.X, squash.Y, squash.Z);
            }
        }
        else if (isOnGround)
        {
            Visuals.Scale = Vector3.One;
        }

        // F-210 Phase 3: Action gestures & Handheld item synchronization
        string? activeItem = NpcActionGestures.ResolveHeldItem(
            State.Inventory,
            State.CurrentAction?.Name,
            State.CurrentTaskSummary,
            NpcState.DefaultStartingMaterial);

        if (CurrentHeldItemId != activeItem)
        {
            SetHeldItem(activeItem);
        }

        GestureState = NpcActionGestures.ResolveGestureState(
            IsActionSwinging,
            !isOnGround,
            Expression,
            State.CurrentAction?.Name,
            State.CurrentTaskSummary);

        // 3. Limb animations: Gestures, Airborne dynamics, and Ground locomotion
        switch (GestureState)
        {
            case NpcGestureState.Swinging:
            {
                _swingTime += delta;
                float swingAngle = NpcActionGestures.CalculateSwingAngle((float)_swingTime);
                RightArmPivot.Rotation = new Vector3(swingAngle, 0f, 0.10f);
                LeftArmPivot.Rotation = LeftArmPivot.Rotation.Lerp(new Vector3(-0.3f, 0f, 0f), (float)(12.0 * delta));

                if (horizontalSpeed > 0.1f && isOnGround)
                {
                    _walkTime += delta * horizontalSpeed * 4.5;
                    float legAngle = Mathf.Sin((float)_walkTime) * 0.6f;
                    LeftLegPivot.Rotation = new Vector3(legAngle, 0, 0);
                    RightLegPivot.Rotation = new Vector3(-legAngle, 0, 0);
                    Visuals.Position = new Vector3(0, Mathf.Abs(Mathf.Sin((float)_walkTime * 2.0f)) * 0.03f, 0);
                }
                else
                {
                    float damp = (float)(10.0 * delta);
                    LeftLegPivot.Rotation = LeftLegPivot.Rotation.Lerp(Vector3.Zero, damp);
                    RightLegPivot.Rotation = RightLegPivot.Rotation.Lerp(Vector3.Zero, damp);
                }
                break;
            }

            case NpcGestureState.ThinkingPose:
            {
                _scratchTime += delta;
                var thinkAngles = NpcActionGestures.CalculateThinkingPoseAngles((float)_scratchTime);
                float thinkDamp = (float)(14.0 * delta);
                RightArmPivot.Rotation = RightArmPivot.Rotation.Lerp(new Vector3(thinkAngles.RightArmRotX, thinkAngles.RightArmRotY, thinkAngles.RightArmRotZ), thinkDamp);
                LeftArmPivot.Rotation = LeftArmPivot.Rotation.Lerp(new Vector3(0.05f, 0f, 0f), thinkDamp);

                float damp = (float)(10.0 * delta);
                LeftLegPivot.Rotation = LeftLegPivot.Rotation.Lerp(Vector3.Zero, damp);
                RightLegPivot.Rotation = RightLegPivot.Rotation.Lerp(Vector3.Zero, damp);
                break;
            }

            case NpcGestureState.TalkingGesticulation:
            {
                _gesticulateTime += delta;
                var gestAngles = NpcActionGestures.CalculateTalkingGesticulationAngles((float)_gesticulateTime);
                float gestDamp = (float)(8.0 * delta);
                RightArmPivot.Rotation = RightArmPivot.Rotation.Lerp(new Vector3(gestAngles.RightArmRotX, 0f, gestAngles.RightArmRotZ), gestDamp);
                LeftArmPivot.Rotation = LeftArmPivot.Rotation.Lerp(new Vector3(gestAngles.LeftArmRotX, 0f, gestAngles.LeftArmRotZ), gestDamp);

                float damp = (float)(10.0 * delta);
                LeftLegPivot.Rotation = LeftLegPivot.Rotation.Lerp(Vector3.Zero, damp);
                RightLegPivot.Rotation = RightLegPivot.Rotation.Lerp(Vector3.Zero, damp);
                break;
            }

            case NpcGestureState.Airborne:
            {
                var airPose = NpcLocomotionDynamics.CalculateAirbornePose(State.Velocity.Y);
                float airDamp = (float)(12.0 * delta);
                LeftArmPivot.Rotation = LeftArmPivot.Rotation.Lerp(new Vector3(airPose.LeftArmRotX, 0f, airPose.LeftArmRotZ), airDamp);
                RightArmPivot.Rotation = RightArmPivot.Rotation.Lerp(new Vector3(airPose.RightArmRotX, 0f, airPose.RightArmRotZ), airDamp);
                LeftLegPivot.Rotation = LeftLegPivot.Rotation.Lerp(new Vector3(airPose.LeftLegRotX, 0f, 0f), airDamp);
                RightLegPivot.Rotation = RightLegPivot.Rotation.Lerp(new Vector3(airPose.RightLegRotX, 0f, 0f), airDamp);
                Visuals.Position = new Vector3(0, 0.02f, 0);
                break;
            }

            case NpcGestureState.None:
            default:
            {
                if (horizontalSpeed > 0.1f && isOnGround)
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
                break;
            }
        }

        // F-210 Phase 1: Head look-at IK, eye blinking, and facial expressions
        UpdateHeadLookAt(delta);
        UpdateBlink(delta);
        UpdateExpressionAndMouth(delta);
    }

    /// <summary>
    /// Computes and interpolates head look-at tracking toward player/target with clamp limits,
    /// or performs subtle idle glancing if no target is within range.
    /// </summary>
    private void UpdateHeadLookAt(double delta)
    {
        if (HeadMesh == null)
        {
            return;
        }

        Vector3? targetWorldPos = TargetLookAtPosition;
        bool tracking = false;

        if (!targetWorldPos.HasValue)
        {
            var cam = GetViewport()?.GetCamera3D();
            if (cam != null)
            {
                float dist = GlobalPosition.DistanceTo(cam.GlobalPosition);
                if (dist <= NpcHeadTracking.DefaultMaxTrackingDistance)
                {
                    bool inDialogue = State != null && State.ConversationState != NpcConversationState.None;
                    Vector3 camToHead = HeadMesh.GlobalPosition - cam.GlobalPosition;
                    Vector3 camFwd = -cam.GlobalTransform.Basis.Z;
                    bool playerLookingAtNpc = camFwd.Dot(camToHead.Normalized()) > 0.65f;
                    bool closeProximity = dist <= 3.0f;

                    if (inDialogue || playerLookingAtNpc || closeProximity)
                    {
                        targetWorldPos = cam.GlobalPosition;
                    }
                }
            }
        }

        if (targetWorldPos.HasValue)
        {
            var headWorldPos = new Vec3(HeadMesh.GlobalPosition.X, HeadMesh.GlobalPosition.Y, HeadMesh.GlobalPosition.Z);
            var targetVec = new Vec3(targetWorldPos.Value.X, targetWorldPos.Value.Y, targetWorldPos.Value.Z);
            float bodyFacing = Rotation.Y;

            var lookResult = NpcHeadTracking.CalculateLookAngles(headWorldPos, bodyFacing, targetVec, NpcHeadTracking.DefaultMaxTrackingDistance);
            if (lookResult.InTrackingRange)
            {
                _targetHeadYaw = lookResult.Yaw;
                _targetHeadPitch = lookResult.Pitch;
                tracking = true;
            }
        }

        IsTrackingTarget = tracking;

        if (!tracking)
        {
            _idleGlanceTimer -= (float)delta;
            if (_idleGlanceTimer <= 0f)
            {
                _idleGlanceTimer = (float)GD.RandRange(2.0, 4.5);
                if (GD.Randf() > 0.4f)
                {
                    _idleTargetYaw = (float)GD.RandRange(-Mathf.DegToRad(12f), Mathf.DegToRad(12f));
                    _idleTargetPitch = (float)GD.RandRange(-Mathf.DegToRad(4f), Mathf.DegToRad(6f));
                }
                else
                {
                    _idleTargetYaw = 0f;
                    _idleTargetPitch = 0f;
                }
            }

            _targetHeadYaw = _idleTargetYaw;
            _targetHeadPitch = _idleTargetPitch;
        }

        float blendSpeed = tracking ? 10.0f : 5.0f;
        _currentHeadYaw = Mathf.LerpAngle(_currentHeadYaw, _targetHeadYaw, (float)(blendSpeed * delta));
        _currentHeadPitch = Mathf.Lerp(_currentHeadPitch, _targetHeadPitch, (float)(blendSpeed * delta));

        HeadMesh.Rotation = new Vector3(_currentHeadPitch, _currentHeadYaw, 0);
        HeadYaw = _currentHeadYaw;
        HeadPitch = _currentHeadPitch;
    }

    /// <summary>
    /// Updates the random blink timer and smooth eye scale animation.
    /// </summary>
    private void UpdateBlink(double delta)
    {
        if (_isBlinking)
        {
            _blinkProgress += (float)delta;
            if (_blinkProgress >= NpcHeadTracking.DefaultBlinkDuration)
            {
                _isBlinking = false;
                _blinkProgress = 0f;
                _blinkScale = 1.0f;
                _blinkTimer = (float)GD.RandRange(NpcHeadTracking.MinBlinkInterval, NpcHeadTracking.MaxBlinkInterval);
            }
            else
            {
                _blinkScale = NpcHeadTracking.CalculateBlinkScale(_blinkProgress, NpcHeadTracking.DefaultBlinkDuration);
            }
        }
        else
        {
            _blinkScale = 1.0f;
            _blinkTimer -= (float)delta;
            if (_blinkTimer <= 0f)
            {
                _isBlinking = true;
                _blinkProgress = 0f;
            }
        }
    }

    /// <summary>
    /// Updates facial expression, squinting, eyebrow position/rotation, and mouth talking oscillation.
    /// </summary>
    private void UpdateExpressionAndMouth(double delta)
    {
        if (FaceRoot == null || LeftEyePivot == null || RightEyePivot == null)
        {
            return;
        }

        bool isTalkingBubble = SpeechBubble != null && SpeechBubble.Visible && !SpeechBubble.IsThinking;
        bool isThinkingBubble = SpeechBubble != null && SpeechBubble.IsThinking;
        var convState = State?.ConversationState ?? NpcConversationState.None;

        Expression = NpcHeadTracking.ResolveExpression(convState, isTalkingBubble, isThinkingBubble);

        Vector3 targetLeftEyeScale = new(1f, _blinkScale, 1f);
        Vector3 targetRightEyeScale = new(1f, _blinkScale, 1f);

        Vector3 targetLeftBrowPos = new(-0.085f, 0.07f, 0f);
        Vector3 targetRightBrowPos = new(0.085f, 0.07f, 0f);
        float targetLeftBrowRotZ = 0f;
        float targetRightBrowRotZ = 0f;

        Vector3 targetMouthPos = new(0f, -0.085f, 0f);
        Vector3 targetMouthScale = new(1f, 1f, 1f);

        switch (Expression)
        {
            case NpcExpressionState.Thinking:
                targetRightBrowPos = new Vector3(0.085f, 0.095f, 0f);
                targetRightBrowRotZ = -0.18f;

                targetLeftBrowPos = new Vector3(-0.085f, 0.055f, 0f);
                targetLeftBrowRotZ = -0.14f;

                targetLeftEyeScale = new Vector3(1f, 0.45f * _blinkScale, 1f);
                targetRightEyeScale = new Vector3(1f, 1.1f * _blinkScale, 1f);

                targetMouthPos = new Vector3(0.015f, -0.085f, 0f);
                targetMouthScale = new Vector3(0.85f, 0.9f, 1f);
                break;

            case NpcExpressionState.Talking:
                _talkTime += delta * 15.0;
                float oscScaleY = 1.0f + 0.8f * Mathf.Abs(Mathf.Sin((float)_talkTime));
                targetMouthScale = new Vector3(1f, oscScaleY, 1f);
                break;

            case NpcExpressionState.Default:
            default:
                break;
        }

        float interp = (float)(15.0 * delta);
        LeftEyePivot.Scale = LeftEyePivot.Scale.Lerp(targetLeftEyeScale, interp);
        RightEyePivot.Scale = RightEyePivot.Scale.Lerp(targetRightEyeScale, interp);

        if (LeftEyebrowPivot != null && RightEyebrowPivot != null)
        {
            LeftEyebrowPivot.Position = LeftEyebrowPivot.Position.Lerp(targetLeftBrowPos, interp);
            RightEyebrowPivot.Position = RightEyebrowPivot.Position.Lerp(targetRightBrowPos, interp);
            LeftEyebrowPivot.Rotation = new Vector3(0, 0, Mathf.Lerp(LeftEyebrowPivot.Rotation.Z, targetLeftBrowRotZ, interp));
            RightEyebrowPivot.Rotation = new Vector3(0, 0, Mathf.Lerp(RightEyebrowPivot.Rotation.Z, targetRightBrowRotZ, interp));
        }

        if (MouthPivot != null)
        {
            MouthPivot.Position = MouthPivot.Position.Lerp(targetMouthPos, interp);
            MouthPivot.Scale = MouthPivot.Scale.Lerp(targetMouthScale, interp);
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

            HairRoot = new Node3D { Name = "HairRoot" };
            HeadMesh.AddChild(HairRoot);
            HeadgearRoot = new Node3D { Name = "HeadgearRoot" };
            HeadMesh.AddChild(HeadgearRoot);

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

            LeftCuffMesh = new MeshInstance3D
            {
                Name = "LeftCuff",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.06f, 0.18f) },
                Position = new Vector3(0, -0.15f, 0)
            };
            LeftArmMesh.AddChild(LeftCuffMesh);

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

            RightCuffMesh = new MeshInstance3D
            {
                Name = "RightCuff",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.06f, 0.18f) },
                Position = new Vector3(0, -0.15f, 0)
            };
            RightArmMesh.AddChild(RightCuffMesh);

            // Hand attachment socket & held item (F-210 Phase 3)
            RightHandSlot = new Node3D { Name = "RightHandSlot", Position = new Vector3(0f, -0.24f, -0.07f) };
            RightArmMesh.AddChild(RightHandSlot);
            HeldItemNode = new Node3D { Name = "HeldItemNode" };
            RightHandSlot.AddChild(HeldItemNode);

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

            LeftBootMesh = new MeshInstance3D
            {
                Name = "LeftBoot",
                Mesh = new BoxMesh { Size = new Vector3(0.20f, 0.22f, 0.21f) },
                Position = new Vector3(0, -0.27f, -0.01f)
            };
            LeftLegMesh.AddChild(LeftBootMesh);

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

            RightBootMesh = new MeshInstance3D
            {
                Name = "RightBoot",
                Mesh = new BoxMesh { Size = new Vector3(0.20f, 0.22f, 0.21f) },
                Position = new Vector3(0, -0.27f, -0.01f)
            };
            RightLegMesh.AddChild(RightBootMesh);

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

            // Build expressive face quads on front of HeadMesh (at local Z = -0.201)
            BuildFaceHierarchy();
        }
        else
        {
            TorsoMesh = Visuals.GetNode<MeshInstance3D>("Torso");
            HeadMesh = Visuals.GetNode<MeshInstance3D>("Head");

            HairRoot = HeadMesh.GetNodeOrNull<Node3D>("HairRoot")!;
            if (HairRoot == null)
            {
                HairRoot = new Node3D { Name = "HairRoot" };
                HeadMesh.AddChild(HairRoot);
            }

            HeadgearRoot = HeadMesh.GetNodeOrNull<Node3D>("HeadgearRoot")!;
            if (HeadgearRoot == null)
            {
                HeadgearRoot = new Node3D { Name = "HeadgearRoot" };
                HeadMesh.AddChild(HeadgearRoot);
            }

            LeftArmPivot = Visuals.GetNode<Node3D>("LeftArmPivot");
            LeftArmMesh = LeftArmPivot.GetNode<MeshInstance3D>("LeftArm");

            LeftCuffMesh = LeftArmMesh.GetNodeOrNull<MeshInstance3D>("LeftCuff")!;
            if (LeftCuffMesh == null)
            {
                LeftCuffMesh = new MeshInstance3D
                {
                    Name = "LeftCuff",
                    Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.06f, 0.18f) },
                    Position = new Vector3(0, -0.15f, 0)
                };
                LeftArmMesh.AddChild(LeftCuffMesh);
            }

            RightArmPivot = Visuals.GetNode<Node3D>("RightArmPivot");
            RightArmMesh = RightArmPivot.GetNode<MeshInstance3D>("RightArm");

            RightCuffMesh = RightArmMesh.GetNodeOrNull<MeshInstance3D>("RightCuff")!;
            if (RightCuffMesh == null)
            {
                RightCuffMesh = new MeshInstance3D
                {
                    Name = "RightCuff",
                    Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.06f, 0.18f) },
                    Position = new Vector3(0, -0.15f, 0)
                };
                RightArmMesh.AddChild(RightCuffMesh);
            }

            RightHandSlot = RightArmMesh.GetNodeOrNull<Node3D>("RightHandSlot")!;
            if (RightHandSlot == null)
            {
                RightHandSlot = new Node3D { Name = "RightHandSlot", Position = new Vector3(0f, -0.24f, -0.07f) };
                RightArmMesh.AddChild(RightHandSlot);
            }
            HeldItemNode = RightHandSlot.GetNodeOrNull<Node3D>("HeldItemNode")!;
            if (HeldItemNode == null)
            {
                HeldItemNode = new Node3D { Name = "HeldItemNode" };
                RightHandSlot.AddChild(HeldItemNode);
            }
            LeftLegPivot = Visuals.GetNode<Node3D>("LeftLegPivot");
            LeftLegMesh = LeftLegPivot.GetNode<MeshInstance3D>("LeftLeg");

            LeftBootMesh = LeftLegMesh.GetNodeOrNull<MeshInstance3D>("LeftBoot")!;
            if (LeftBootMesh == null)
            {
                LeftBootMesh = new MeshInstance3D
                {
                    Name = "LeftBoot",
                    Mesh = new BoxMesh { Size = new Vector3(0.20f, 0.22f, 0.21f) },
                    Position = new Vector3(0, -0.27f, -0.01f)
                };
                LeftLegMesh.AddChild(LeftBootMesh);
            }

            RightLegPivot = Visuals.GetNode<Node3D>("RightLegPivot");
            RightLegMesh = RightLegPivot.GetNode<MeshInstance3D>("RightLeg");

            RightBootMesh = RightLegMesh.GetNodeOrNull<MeshInstance3D>("RightBoot")!;
            if (RightBootMesh == null)
            {
                RightBootMesh = new MeshInstance3D
                {
                    Name = "RightBoot",
                    Mesh = new BoxMesh { Size = new Vector3(0.20f, 0.22f, 0.21f) },
                    Position = new Vector3(0, -0.27f, -0.01f)
                };
                RightLegMesh.AddChild(RightBootMesh);
            }

            NameTag = GetNode<Label3D>("NameTag");
            SpeechBubble = GetNodeOrNull<SpeechBubble3D>("SpeechBubble")!;
            if (SpeechBubble == null)
            {
                SpeechBubble = new SpeechBubble3D { Name = "SpeechBubble", Position = new Vector3(0, 2.30f, 0) };
                AddChild(SpeechBubble);
            }

            FaceRoot = HeadMesh.GetNodeOrNull<Node3D>("FaceRoot")!;
            if (FaceRoot == null)
            {
                BuildFaceHierarchy();
            }
            else
            {
                LeftEyePivot = FaceRoot.GetNode<Node3D>("LeftEyePivot");
                LeftEyeMesh = LeftEyePivot.GetNode<MeshInstance3D>("LeftEye");
                LeftPupilMesh = LeftEyePivot.GetNode<MeshInstance3D>("LeftPupil");
                RightEyePivot = FaceRoot.GetNode<Node3D>("RightEyePivot");
                RightEyeMesh = RightEyePivot.GetNode<MeshInstance3D>("RightEye");
                RightPupilMesh = RightEyePivot.GetNode<MeshInstance3D>("RightPupil");
                LeftEyebrowPivot = FaceRoot.GetNode<Node3D>("LeftEyebrowPivot");
                LeftEyebrowMesh = LeftEyebrowPivot.GetNode<MeshInstance3D>("LeftEyebrow");
                RightEyebrowPivot = FaceRoot.GetNode<Node3D>("RightEyebrowPivot");
                RightEyebrowMesh = RightEyebrowPivot.GetNode<MeshInstance3D>("RightEyebrow");
                MouthPivot = FaceRoot.GetNode<Node3D>("MouthPivot");
                MouthMesh = MouthPivot.GetNode<MeshInstance3D>("Mouth");
            }
        }

        _isBuilt = true;
        ApplyAppearance();
    }

    /// <summary>
    /// Constructs low-poly quads for eyes (sclera + pupils), eyebrows, and mouth on the front face of the head.
    /// </summary>
    private void BuildFaceHierarchy()
    {
        FaceRoot = new Node3D
        {
            Name = "FaceRoot",
            Position = new Vector3(0, 0, -0.201f)
        };
        HeadMesh.AddChild(FaceRoot);

        var scleraMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.96f, 0.96f, 0.98f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 1
        };

        var pupilMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.12f, 0.16f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 2
        };

        var eyebrowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.20f, 0.15f, 0.12f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 1
        };

        var mouthMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.50f, 0.22f, 0.22f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 1
        };

        // Left Eye (-0.085, 0.02, 0)
        LeftEyePivot = new Node3D { Name = "LeftEyePivot", Position = new Vector3(-0.085f, 0.02f, 0) };
        LeftEyeMesh = new MeshInstance3D
        {
            Name = "LeftEye",
            Mesh = new QuadMesh { Size = new Vector2(0.065f, 0.055f) },
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = scleraMaterial
        };
        LeftPupilMesh = new MeshInstance3D
        {
            Name = "LeftPupil",
            Mesh = new QuadMesh { Size = new Vector2(0.032f, 0.038f) },
            Position = new Vector3(0, -0.003f, -0.001f),
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = pupilMaterial
        };
        LeftEyePivot.AddChild(LeftEyeMesh);
        LeftEyePivot.AddChild(LeftPupilMesh);
        FaceRoot.AddChild(LeftEyePivot);

        // Right Eye (0.085, 0.02, 0)
        RightEyePivot = new Node3D { Name = "RightEyePivot", Position = new Vector3(0.085f, 0.02f, 0) };
        RightEyeMesh = new MeshInstance3D
        {
            Name = "RightEye",
            Mesh = new QuadMesh { Size = new Vector2(0.065f, 0.055f) },
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = scleraMaterial
        };
        RightPupilMesh = new MeshInstance3D
        {
            Name = "RightPupil",
            Mesh = new QuadMesh { Size = new Vector2(0.032f, 0.038f) },
            Position = new Vector3(0, -0.003f, -0.001f),
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = pupilMaterial
        };
        RightEyePivot.AddChild(RightEyeMesh);
        RightEyePivot.AddChild(RightPupilMesh);
        FaceRoot.AddChild(RightEyePivot);

        // Left Eyebrow (-0.085, 0.07, 0)
        LeftEyebrowPivot = new Node3D { Name = "LeftEyebrowPivot", Position = new Vector3(-0.085f, 0.07f, 0) };
        LeftEyebrowMesh = new MeshInstance3D
        {
            Name = "LeftEyebrow",
            Mesh = new QuadMesh { Size = new Vector2(0.075f, 0.02f) },
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = eyebrowMaterial
        };
        LeftEyebrowPivot.AddChild(LeftEyebrowMesh);
        FaceRoot.AddChild(LeftEyebrowPivot);

        // Right Eyebrow (0.085, 0.07, 0)
        RightEyebrowPivot = new Node3D { Name = "RightEyebrowPivot", Position = new Vector3(0.085f, 0.07f, 0) };
        RightEyebrowMesh = new MeshInstance3D
        {
            Name = "RightEyebrow",
            Mesh = new QuadMesh { Size = new Vector2(0.075f, 0.02f) },
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = eyebrowMaterial
        };
        RightEyebrowPivot.AddChild(RightEyebrowMesh);
        FaceRoot.AddChild(RightEyebrowPivot);

        // Mouth (0, -0.085, 0)
        MouthPivot = new Node3D { Name = "MouthPivot", Position = new Vector3(0, -0.085f, 0) };
        MouthMesh = new MeshInstance3D
        {
            Name = "Mouth",
            Mesh = new QuadMesh { Size = new Vector2(0.085f, 0.028f) },
            Rotation = new Vector3(0, Mathf.Pi, 0),
            MaterialOverride = mouthMaterial
        };
        MouthPivot.AddChild(MouthMesh);
        FaceRoot.AddChild(MouthPivot);
    }

    /// <summary>
    /// Displays a speech message in the speech bubble floating above the NPC.
    /// </summary>
    public void ShowSpeechBubble(string text, float duration = SpeechBubble3D.DefaultDuration)
    {
        EnsureVisualHierarchy();
        SpeechBubble?.ShowSpeech(text, duration);
    }

    /// <summary>
    /// Updates whether the floating speech bubble shows an animated thinking indicator.
    /// </summary>
    public void SetThinking(bool isThinking)
    {
        EnsureVisualHierarchy();
        SpeechBubble?.SetThinking(isThinking);
    }

    /// <summary>
    /// Updates the 3D miniature item or tool model rendered in the NPC's right hand (F-210 Phase 3).
    /// Supports miniature voxel blocks and voxel pickaxe tool models.
    /// </summary>
    public void SetHeldItem(string? itemId)
    {
        EnsureVisualHierarchy();
        CurrentHeldItemId = itemId;

        if (HeldItemNode == null)
        {
            return;
        }

        // Clear existing held item children
        foreach (var child in HeldItemNode.GetChildren())
        {
            HeldItemNode.RemoveChild(child);
            child.QueueFree();
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            HeldItemNode.Visible = false;
            return;
        }

        HeldItemNode.Visible = true;

        if (itemId.Contains("pickaxe", StringComparison.OrdinalIgnoreCase) ||
            itemId.Contains("tool", StringComparison.OrdinalIgnoreCase))
        {
            // Pickaxe tool model
            HeldItemNode.Rotation = new Vector3(-0.35f, 0f, 0f);

            var handleMesh = new MeshInstance3D
            {
                Name = "Handle",
                Mesh = new BoxMesh { Size = new Vector3(0.03f, 0.22f, 0.03f) },
                Position = new Vector3(0f, 0.06f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.38f, 0.22f) }
            };
            HeldItemNode.AddChild(handleMesh);

            var headMesh = new MeshInstance3D
            {
                Name = "ToolHead",
                Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.04f, 0.045f) },
                Position = new Vector3(0f, 0.16f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.74f, 0.78f) }
            };
            HeldItemNode.AddChild(headMesh);
        }
        else
        {
            // Miniature voxel block
            HeldItemNode.Rotation = Vector3.Zero;

            Color blockColor = itemId.ToLowerInvariant() switch
            {
                "dirt" => new Color(0.48f, 0.32f, 0.20f),
                "wood" or "planks" => new Color(0.58f, 0.40f, 0.25f),
                "grass" or "leaves" => new Color(0.35f, 0.65f, 0.25f),
                "sand" => new Color(0.85f, 0.80f, 0.55f),
                "iron_ore" => new Color(0.65f, 0.55f, 0.50f),
                _ => new Color(0.52f, 0.52f, 0.55f) // cobblestone / stone
            };

            var blockMesh = new MeshInstance3D
            {
                Name = "Block",
                Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) },
                Position = new Vector3(0f, -0.02f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = blockColor }
            };
            HeldItemNode.AddChild(blockMesh);
        }
    }

    /// <summary>
    /// Builds or updates procedural hair volume and archetype headgear (F-210 Phase 4).
    /// </summary>
    public void BuildHairAndHeadgear(NpcAppearanceDetails details)
    {
        if (HeadMesh == null)
        {
            return;
        }

        if (HairRoot == null)
        {
            HairRoot = new Node3D { Name = "HairRoot" };
            HeadMesh.AddChild(HairRoot);
        }
        else
        {
            foreach (var child in HairRoot.GetChildren())
            {
                HairRoot.RemoveChild(child);
                child.QueueFree();
            }
        }

        if (HeadgearRoot == null)
        {
            HeadgearRoot = new Node3D { Name = "HeadgearRoot" };
            HeadMesh.AddChild(HeadgearRoot);
        }
        else
        {
            foreach (var child in HeadgearRoot.GetChildren())
            {
                HeadgearRoot.RemoveChild(child);
                child.QueueFree();
            }
        }

        var hairMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(details.HairColor.R, details.HairColor.G, details.HairColor.B)
        };

        // Hair Layer (covers top, back, sides, and high fringe)
        var hairTop = new MeshInstance3D
        {
            Name = "HairTop",
            Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.08f, 0.42f) },
            Position = new Vector3(0, 0.18f, 0),
            MaterialOverride = hairMaterial
        };
        HairRoot.AddChild(hairTop);

        var hairBack = new MeshInstance3D
        {
            Name = "HairBack",
            Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.22f, 0.05f) },
            Position = new Vector3(0, 0.04f, 0.19f),
            MaterialOverride = hairMaterial
        };
        HairRoot.AddChild(hairBack);

        var hairLeft = new MeshInstance3D
        {
            Name = "HairLeft",
            Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.20f, 0.38f) },
            Position = new Vector3(-0.19f, 0.04f, 0.01f),
            MaterialOverride = hairMaterial
        };
        HairRoot.AddChild(hairLeft);

        var hairRight = new MeshInstance3D
        {
            Name = "HairRight",
            Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.20f, 0.38f) },
            Position = new Vector3(0.19f, 0.04f, 0.01f),
            MaterialOverride = hairMaterial
        };
        HairRoot.AddChild(hairRight);

        var hairBangs = new MeshInstance3D
        {
            Name = "HairBangs",
            Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.05f, 0.04f) },
            Position = new Vector3(0, 0.17f, -0.19f),
            MaterialOverride = hairMaterial
        };
        HairRoot.AddChild(hairBangs);

        // Headgear Layer based on archetype
        var headgearMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(details.HeadgearColor.R, details.HeadgearColor.G, details.HeadgearColor.B)
        };
        var accentMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(details.HeadgearAccentColor.R, details.HeadgearAccentColor.G, details.HeadgearAccentColor.B)
        };

        switch (details.Headgear)
        {
            case NpcHeadgearType.StrawHat:
            {
                var brim = new MeshInstance3D
                {
                    Name = "StrawHatBrim",
                    Mesh = new BoxMesh { Size = new Vector3(0.62f, 0.04f, 0.62f) },
                    Position = new Vector3(0, 0.20f, 0),
                    MaterialOverride = headgearMat
                };
                HeadgearRoot.AddChild(brim);

                var crown = new MeshInstance3D
                {
                    Name = "StrawHatCrown",
                    Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.12f, 0.34f) },
                    Position = new Vector3(0, 0.28f, 0),
                    MaterialOverride = headgearMat
                };
                HeadgearRoot.AddChild(crown);

                var ribbon = new MeshInstance3D
                {
                    Name = "StrawHatRibbon",
                    Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.04f, 0.35f) },
                    Position = new Vector3(0, 0.24f, 0),
                    MaterialOverride = accentMat
                };
                HeadgearRoot.AddChild(ribbon);
                break;
            }

            case NpcHeadgearType.MinerHelmet:
            {
                var dome = new MeshInstance3D
                {
                    Name = "MinerHelmetDome",
                    Mesh = new BoxMesh { Size = new Vector3(0.44f, 0.12f, 0.44f) },
                    Position = new Vector3(0, 0.22f, 0),
                    MaterialOverride = headgearMat
                };
                HeadgearRoot.AddChild(dome);

                var lamp = new MeshInstance3D
                {
                    Name = "MinerHelmetLamp",
                    Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.06f) },
                    Position = new Vector3(0, 0.19f, -0.23f),
                    MaterialOverride = accentMat
                };
                HeadgearRoot.AddChild(lamp);
                break;
            }

            case NpcHeadgearType.Headband:
            {
                var band = new MeshInstance3D
                {
                    Name = "HeadbandStrap",
                    Mesh = new BoxMesh { Size = new Vector3(0.43f, 0.06f, 0.43f) },
                    Position = new Vector3(0, 0.15f, 0),
                    MaterialOverride = headgearMat
                };
                HeadgearRoot.AddChild(band);

                var buckle = new MeshInstance3D
                {
                    Name = "HeadbandBuckle",
                    Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.03f) },
                    Position = new Vector3(0, 0.15f, -0.21f),
                    MaterialOverride = accentMat
                };
                HeadgearRoot.AddChild(buckle);
                break;
            }

            case NpcHeadgearType.Cap:
            {
                var crown = new MeshInstance3D
                {
                    Name = "CapCrown",
                    Mesh = new BoxMesh { Size = new Vector3(0.43f, 0.09f, 0.43f) },
                    Position = new Vector3(0, 0.22f, 0),
                    MaterialOverride = headgearMat
                };
                HeadgearRoot.AddChild(crown);

                var visor = new MeshInstance3D
                {
                    Name = "CapVisor",
                    Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.03f, 0.14f) },
                    Position = new Vector3(0, 0.19f, -0.26f),
                    MaterialOverride = accentMat
                };
                HeadgearRoot.AddChild(visor);
                break;
            }

            case NpcHeadgearType.HairOnly:
            case NpcHeadgearType.None:
            default:
                break;
        }
    }
}

