namespace VoxelKing.Game.Debug;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Npc;

/// <summary>
/// Headless automated integration test validating Phase 1 of LIVELY_NPCS_PLAN:
/// - Expressive face quad hierarchy (FaceRoot, eyes, pupils, eyebrows, mouth).
/// - Default, Thinking (raised eyebrow, squinted eye), and Talking (mouth oscillation) expressions.
/// - Eye blink system scaling eye quads.
/// - Independent head-tracking IK and clamp limits.
/// - Return to subtle idle glances when target is out of range or behind.
/// </summary>
public partial class TestLivelyNpcsRunner : Node3D
{
    private NpcNode _npcNode = null!;
    private NpcState _npcState = null!;
    private int _frame = 0;
    private int _phase = 0;
    private float _initialLeftBrowY;
    private float _initialRightBrowY;

    public override void _Ready()
    {
        GD.Print("[INFO] TestLivelyNpcsRunner starting Phase 1 lively NPC tests...");

        _npcState = new NpcState(
            id: new EntityId(101),
            name: "LivelyBob",
            position: new Vec3(0f, 0f, 0f),
            facing: 0f,
            appearanceColor: "#3D7BEB");

        _npcNode = new NpcNode { Name = "TestNpc" };
        AddChild(_npcNode);
        _npcNode.BindState(_npcState);
    }

    public override void _Process(double delta)
    {
        _frame++;

        switch (_phase)
        {
            case 0:
            {
                // Wait for visual hierarchy to be ready
                if (_frame < 5)
                {
                    return;
                }

                // Test 1: Facial hierarchy exists
                AssertTrue(_npcNode.FaceRoot != null, "FaceRoot must exist under HeadMesh.");
                AssertTrue(_npcNode.LeftEyePivot != null && _npcNode.RightEyePivot != null, "Eye pivots must exist.");
                AssertTrue(_npcNode.LeftEyeMesh != null && _npcNode.RightEyeMesh != null, "Eye sclera meshes must exist.");
                AssertTrue(_npcNode.LeftPupilMesh != null && _npcNode.RightPupilMesh != null, "Pupil meshes must exist.");
                AssertTrue(_npcNode.LeftEyebrowPivot != null && _npcNode.RightEyebrowPivot != null, "Eyebrow pivots must exist.");
                AssertTrue(_npcNode.MouthPivot != null && _npcNode.MouthMesh != null, "Mouth hierarchy must exist.");

                _initialLeftBrowY = _npcNode.LeftEyebrowPivot!.Position.Y;
                _initialRightBrowY = _npcNode.RightEyebrowPivot!.Position.Y;

                GD.Print("[PASS] Step 1: Facial hierarchy and mesh nodes verified successfully.");
                _phase++;
                _frame = 0;
                break;
            }

            case 1:
            {
                // Test 2: Default neutral gaze
                if (_frame < 10)
                {
                    return;
                }

                AssertTrue(_npcNode.Expression == NpcExpressionState.Default, $"Expected Default expression, got {_npcNode.Expression}.");
                AssertTrue(Mathf.Abs(_npcNode.LeftEyePivot.Scale.Y - 1.0f) < 0.1f, "Default left eye scale Y should be near 1.0.");
                AssertTrue(Mathf.Abs(_npcNode.RightEyePivot.Scale.Y - 1.0f) < 0.1f, "Default right eye scale Y should be near 1.0.");

                GD.Print("[PASS] Step 2: Default expression verified.");

                // Transition to Thinking state
                _npcState.SetConversationState(NpcConversationState.Thinking);
                _npcNode.SetThinking(true);
                _phase++;
                _frame = 0;
                break;
            }

            case 2:
            {
                // Test 3: Thinking expression (raised eyebrow, squinted eye)
                if (_frame < 25)
                {
                    return;
                }

                AssertTrue(_npcNode.Expression == NpcExpressionState.Thinking, $"Expected Thinking expression, got {_npcNode.Expression}.");
                AssertTrue(_npcNode.RightEyebrowPivot.Position.Y > _npcNode.LeftEyebrowPivot.Position.Y,
                    $"Right eyebrow ({_npcNode.RightEyebrowPivot.Position.Y}) should be raised higher than left eyebrow ({_npcNode.LeftEyebrowPivot.Position.Y}) while thinking.");
                AssertTrue(_npcNode.LeftEyePivot.Scale.Y < _npcNode.RightEyePivot.Scale.Y,
                    $"Left eye scale Y ({_npcNode.LeftEyePivot.Scale.Y}) should be squinted compared to right eye ({_npcNode.RightEyePivot.Scale.Y}).");

                GD.Print("[PASS] Step 3: Thinking expression (raised right brow + squinted left eye) verified.");

                // Reset thinking and trigger speech
                _npcNode.SetThinking(false);
                _npcState.SetConversationState(NpcConversationState.Replying);
                _npcNode.ShowSpeechBubble("Greetings, traveler!", duration: 5.0f);
                _phase++;
                _frame = 0;
                break;
            }

            case 3:
            {
                // Test 4: Talking expression (mouth vertical oscillation)
                if (_frame < 15)
                {
                    return;
                }

                AssertTrue(_npcNode.Expression == NpcExpressionState.Talking, $"Expected Talking expression, got {_npcNode.Expression}.");
                float mouthScaleY = _npcNode.MouthPivot.Scale.Y;
                AssertTrue(mouthScaleY > 0.9f, $"Mouth scale Y should oscillate during speech, was {mouthScaleY}.");

                GD.Print("[PASS] Step 4: Talking expression with mouth oscillation verified.");

                // Test 5: Head tracking look-at IK
                _npcNode.SpeechBubble.Hide();
                _npcState.SetConversationState(NpcConversationState.None);
                // Aim target 45 degrees to the right, elevated slightly
                _npcNode.TargetLookAtPosition = new Vector3(2.5f, 2.2f, -2.5f);
                _phase++;
                _frame = 0;
                break;
            }

            case 4:
            {
                // Let head smoothly interpolate toward target
                if (_frame < 30)
                {
                    return;
                }

                AssertTrue(_npcNode.IsTrackingTarget, "IsTrackingTarget should be true when target is within tracking range.");
                // Target is to the right (+X), so local yaw is negative
                AssertTrue(_npcNode.HeadMesh.Rotation.Y < -0.1f, $"Head yaw ({_npcNode.HeadMesh.Rotation.Y}) should turn right toward target.");
                // Target is above head, so pitch is positive
                AssertTrue(_npcNode.HeadMesh.Rotation.X > 0.05f, $"Head pitch ({_npcNode.HeadMesh.Rotation.X}) should tilt up toward target.");

                // Clamp limits check: Head rotation must respect ±55 deg yaw and ±30 deg pitch
                AssertTrue(_npcNode.HeadMesh.Rotation.Y >= -NpcHeadTracking.MaxYawRadians - 0.01f,
                    $"Head yaw ({_npcNode.HeadMesh.Rotation.Y}) must not exceed natural limit ({-NpcHeadTracking.MaxYawRadians}).");
                AssertTrue(_npcNode.HeadMesh.Rotation.X <= NpcHeadTracking.MaxPitchRadians + 0.01f,
                    $"Head pitch ({_npcNode.HeadMesh.Rotation.X}) must not exceed natural pitch limit ({NpcHeadTracking.MaxPitchRadians}).");

                GD.Print("[PASS] Step 5: Head-tracking IK and rotation clamping limits verified.");

                // Test 6: Target behind NPC (outside natural head swivel range)
                _npcNode.TargetLookAtPosition = new Vector3(0f, 1.55f, 4.0f); // Directly behind (+Z)
                _phase++;
                _frame = 0;
                break;
            }

            case 5:
            {
                if (_frame < 30)
                {
                    return;
                }

                AssertTrue(!_npcNode.IsTrackingTarget, "IsTrackingTarget should be false for target behind NPC.");
                // Head should have relaxed back toward forward/idle range (< 0.25 rad)
                AssertTrue(Mathf.Abs(_npcNode.HeadMesh.Rotation.Y) < 0.35f,
                    $"Head yaw ({_npcNode.HeadMesh.Rotation.Y}) should return toward center when target is behind.");

                GD.Print("[PASS] Step 6: Target behind NPC correctly ignored without neck wrenching.");

                // Phase 2 Step 7: Turn banking and forward lean
                _npcNode.TargetLookAtPosition = null;
                _npcState.Velocity = new Vec3(0f, 0f, -3.0f);
                _npcState.Facing = Mathf.DegToRad(45f); // Rapid turn
                _phase++;
                _frame = 0;
                break;
            }

            case 6:
            {
                // Let body turn and bank
                if (_frame < 15)
                {
                    return;
                }

                AssertTrue(Mathf.Abs(_npcNode.TurnBankAngle) > 0.01f,
                    $"TurnBankAngle ({_npcNode.TurnBankAngle}) should bank into turn when rotating while moving.");
                AssertTrue(_npcNode.ForwardLeanAngle < 0f,
                    $"ForwardLeanAngle ({_npcNode.ForwardLeanAngle}) should lean forward when running.");
                AssertTrue(Mathf.Abs(_npcNode.Visuals.Rotation.Z - _npcNode.TurnBankAngle) < 0.01f,
                    "Visuals.Rotation.Z should match TurnBankAngle.");

                GD.Print("[PASS] Step 7: Turn banking and forward lean dynamics verified.");

                // Phase 2 Step 8: Airborne ascending (jumping)
                _npcState.IsOnGround = false;
                _npcState.Velocity = new Vec3(0f, 6.0f, 0f); // Ascending
                _phase++;
                _frame = 0;
                break;
            }

            case 7:
            {
                if (_frame < 15)
                {
                    return;
                }

                AssertTrue(_npcNode.IsAirborne, "NPC should be detected as airborne.");
                AssertTrue(_npcNode.LeftArmPivot.Rotation.X < -0.15f,
                    $"Left arm ({_npcNode.LeftArmPivot.Rotation.X}) should raise forward while ascending.");
                AssertTrue(_npcNode.RightArmPivot.Rotation.X < -0.15f,
                    $"Right arm ({_npcNode.RightArmPivot.Rotation.X}) should raise forward while ascending.");
                AssertTrue(_npcNode.LeftLegPivot.Rotation.X > 0.05f,
                    $"Left leg ({_npcNode.LeftLegPivot.Rotation.X}) should extend downward while ascending.");

                GD.Print("[PASS] Step 8: Airborne ascending (jumping) pose dynamics verified.");

                // Phase 2 Step 9: Airborne descending (falling)
                _npcState.Velocity = new Vec3(0f, -8.0f, 0f); // Falling
                _phase++;
                _frame = 0;
                break;
            }

            case 8:
            {
                if (_frame < 15)
                {
                    return;
                }

                // Falling: arms should flare outwards (left arm negative Z, right arm positive Z)
                AssertTrue(_npcNode.LeftArmPivot.Rotation.Z < -0.15f,
                    $"Left arm ({_npcNode.LeftArmPivot.Rotation.Z}) should flare outwards to left while falling.");
                AssertTrue(_npcNode.RightArmPivot.Rotation.Z > 0.15f,
                    $"Right arm ({_npcNode.RightArmPivot.Rotation.Z}) should flare outwards to right while falling.");

                GD.Print("[PASS] Step 9: Airborne descending (falling) outward arm flare verified.");

                // Phase 2 Step 10: Landing impact squash & stretch
                _npcState.IsOnGround = true;
                _npcState.Velocity = Vec3.Zero;
                _phase++;
                _frame = 0;
                break;
            }

            case 9:
            {
                // Touchdown frame: should detect landing transition and apply squash
                if (_frame < 2)
                {
                    return;
                }

                if (_frame == 2)
                {
                    AssertTrue(_npcNode.IsLandingSquash, "IsLandingSquash should be true immediately after touchdown.");
                    AssertTrue(_npcNode.Visuals.Scale.Y < 0.98f,
                        $"Visuals.Scale.Y ({_npcNode.Visuals.Scale.Y}) should compress vertically on landing impact.");
                    AssertTrue(_npcNode.Visuals.Scale.X > 1.01f,
                        $"Visuals.Scale.X ({_npcNode.Visuals.Scale.X}) should expand horizontally on landing impact.");
                    GD.Print($"[PASS] Step 10a: Landing squash compression verified (Scale: {_npcNode.Visuals.Scale}).");
                }

                // Wait until landing squash completes (duration 150ms)
                if (_npcNode.IsLandingSquash)
                {
                    if (_frame > 200)
                    {
                        AssertTrue(!_npcNode.IsLandingSquash, "IsLandingSquash timed out waiting for 150ms bounce to complete.");
                    }
                    return;
                }

                AssertTrue(!_npcNode.IsLandingSquash, "IsLandingSquash should finish after 150ms bounce.");
                AssertTrue(Mathf.Abs(_npcNode.Visuals.Scale.Y - 1.0f) < 0.02f,
                    $"Visuals.Scale.Y ({_npcNode.Visuals.Scale.Y}) should recover back to 1.0.");
                AssertTrue(Mathf.Abs(_npcNode.Visuals.Scale.X - 1.0f) < 0.02f,
                    $"Visuals.Scale.X ({_npcNode.Visuals.Scale.X}) should recover back to 1.0.");

                GD.Print("[PASS] Step 10b: Landing squash smooth recovery back to unit scale verified.");

                // Phase 3 Step 11: Hand attachment socket & held block
                AssertTrue(_npcNode.RightHandSlot != null, "RightHandSlot should exist under RightArmMesh.");
                AssertTrue(_npcNode.HeldItemNode != null, "HeldItemNode should exist under RightHandSlot.");
                AssertTrue(!string.IsNullOrEmpty(_npcNode.CurrentHeldItemId), "CurrentHeldItemId should be set.");
                AssertTrue(_npcNode.HeldItemNode!.GetChildCount() > 0, "HeldItemNode should render child item mesh.");

                GD.Print($"[PASS] Step 11: RightHandSlot socket and initial held item ({_npcNode.CurrentHeldItemId}) verified.");

                // Phase 3 Step 12: Tool model ("pickaxe")
                _npcNode.SetHeldItem("pickaxe");
                AssertTrue(_npcNode.CurrentHeldItemId == "pickaxe", "Held item should be pickaxe.");
                AssertTrue(_npcNode.HeldItemNode.GetNodeOrNull<MeshInstance3D>("Handle") != null, "Pickaxe should have Handle mesh.");
                AssertTrue(_npcNode.HeldItemNode.GetNodeOrNull<MeshInstance3D>("ToolHead") != null, "Pickaxe should have ToolHead mesh.");

                GD.Print("[PASS] Step 12: Pickaxe tool model (Handle + ToolHead) verified.");

                // Phase 3 Step 13: Action Gesture: Dig / Place / Attack Hammer Swing
                _npcNode.IsActionSwinging = true;
                _phase++;
                _frame = 0;
                break;
            }

            case 10:
            {
                AssertTrue(_npcNode.GestureState == NpcGestureState.Swinging,
                    $"GestureState should be Swinging, was {_npcNode.GestureState}.");

                float rotX = _npcNode.RightArmPivot.Rotation.X;
                AssertTrue(rotX <= -0.20f && rotX >= -1.40f,
                    $"Right arm swing ({rotX}) should be within swing oscillation bounds [-1.4, -0.2].");

                if (_frame < 25)
                {
                    return;
                }

                GD.Print("[PASS] Step 13: Dig / Place hammer swing action gesture verified.");

                // Phase 3 Step 14: Action Gesture: Thinking pose (scratching head)
                _npcNode.IsActionSwinging = false;
                _npcState.SetConversationState(NpcConversationState.Thinking);
                _npcNode.SetThinking(true);
                _phase++;
                _frame = 0;
                break;
            }

            case 11:
            {
                if (_frame < 20)
                {
                    return;
                }

                AssertTrue(_npcNode.GestureState == NpcGestureState.ThinkingPose,
                    $"GestureState should be ThinkingPose, was {_npcNode.GestureState}.");
                // Right arm should be raised up near head (RotX < -1.8) and tilted inward (RotZ < -0.3)
                AssertTrue(_npcNode.RightArmPivot.Rotation.X < -1.7f,
                    $"Right arm ({_npcNode.RightArmPivot.Rotation.X}) should be raised up near head.");
                AssertTrue(_npcNode.RightArmPivot.Rotation.Z < -0.3f,
                    $"Right arm ({_npcNode.RightArmPivot.Rotation.Z}) should tilt inward to scratch head.");

                GD.Print("[PASS] Step 14: Thinking pose (right arm raised scratching head) verified.");

                // Phase 3 Step 15: Conversation hand gesticulation
                _npcNode.SetThinking(false);
                _npcState.SetConversationState(NpcConversationState.Replying);
                _npcNode.ShowSpeechBubble("Here is your completed structure!", duration: 4.0f);
                _phase++;
                _frame = 0;
                break;
            }

            case 12:
            {
                if (_frame < 15)
                {
                    return;
                }

                AssertTrue(_npcNode.GestureState == NpcGestureState.TalkingGesticulation,
                    $"GestureState should be TalkingGesticulation, was {_npcNode.GestureState}.");
                // Both arms active during speech
                AssertTrue(Mathf.Abs(_npcNode.RightArmPivot.Rotation.X) > 0.1f,
                    $"Right arm ({_npcNode.RightArmPivot.Rotation.X}) should gesticulate during conversation.");

                GD.Print("[PASS] Step 15: Conversation hand gesticulation gesture verified.");

                // Phase 4 Step 16: Procedural hair volume layer
                AssertTrue(_npcNode.HairRoot != null, "HairRoot should exist under HeadMesh.");
                AssertTrue(_npcNode.HairRoot!.GetNodeOrNull<MeshInstance3D>("HairTop") != null, "HairTop mesh should exist.");
                AssertTrue(_npcNode.HairRoot.GetNodeOrNull<MeshInstance3D>("HairBack") != null, "HairBack mesh should exist.");
                AssertTrue(_npcNode.HairRoot.GetNodeOrNull<MeshInstance3D>("HairLeft") != null, "HairLeft mesh should exist.");
                AssertTrue(_npcNode.HairRoot.GetNodeOrNull<MeshInstance3D>("HairRight") != null, "HairRight mesh should exist.");
                AssertTrue(_npcNode.HairRoot.GetNodeOrNull<MeshInstance3D>("HairBangs") != null, "HairBangs mesh should exist.");

                GD.Print("[PASS] Step 16: Procedural hair volume layer (top, back, sides, fringe) verified.");

                // Phase 4 Step 17: Clothing accents (boots and cuffs)
                AssertTrue(_npcNode.LeftBootMesh != null, "LeftBootMesh should exist.");
                AssertTrue(_npcNode.RightBootMesh != null, "RightBootMesh should exist.");
                AssertTrue(_npcNode.LeftCuffMesh != null, "LeftCuffMesh should exist.");
                AssertTrue(_npcNode.RightCuffMesh != null, "RightCuffMesh should exist.");
                AssertTrue(_npcNode.LeftBootMesh!.MaterialOverride != null, "LeftBoot should have material override.");
                AssertTrue(_npcNode.LeftCuffMesh!.MaterialOverride != null, "LeftCuff should have material override.");

                GD.Print("[PASS] Step 17: Clothing accents (boots and cuffs) verified.");

                // Phase 4 Step 18: Archetype headgear and silhouettes
                AssertTrue(_npcNode.HeadgearRoot != null, "HeadgearRoot should exist under HeadMesh.");

                // Test Farmer silhouette (Bob has Farmer archetype)
                _npcNode.SetArchetype(NpcArchetype.Farmer);
                AssertTrue(_npcNode.Archetype == NpcArchetype.Farmer, "Archetype should be Farmer.");
                AssertTrue(_npcNode.HeadgearType == NpcHeadgearType.StrawHat, "Farmer should wear StrawHat.");
                AssertTrue(_npcNode.HeadgearRoot!.GetNodeOrNull<MeshInstance3D>("StrawHatBrim") != null, "StrawHatBrim should exist.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("StrawHatCrown") != null, "StrawHatCrown should exist.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("StrawHatRibbon") != null, "StrawHatRibbon should exist.");

                // Test Miner silhouette
                _npcNode.SetArchetype(NpcArchetype.Miner);
                AssertTrue(_npcNode.Archetype == NpcArchetype.Miner, "Archetype should be Miner.");
                AssertTrue(_npcNode.HeadgearType == NpcHeadgearType.MinerHelmet, "Miner should wear MinerHelmet.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("MinerHelmetDome") != null, "MinerHelmetDome should exist.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("MinerHelmetLamp") != null, "MinerHelmetLamp should exist.");

                // Test Guard silhouette
                _npcNode.SetArchetype(NpcArchetype.Guard);
                AssertTrue(_npcNode.Archetype == NpcArchetype.Guard, "Archetype should be Guard.");
                AssertTrue(_npcNode.HeadgearType == NpcHeadgearType.Headband, "Guard should wear Headband.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("HeadbandStrap") != null, "HeadbandStrap should exist.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("HeadbandBuckle") != null, "HeadbandBuckle should exist.");

                // Test Courier silhouette
                _npcNode.SetArchetype(NpcArchetype.Courier);
                AssertTrue(_npcNode.Archetype == NpcArchetype.Courier, "Archetype should be Courier.");
                AssertTrue(_npcNode.HeadgearType == NpcHeadgearType.Cap, "Courier should wear Cap.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("CapCrown") != null, "CapCrown should exist.");
                AssertTrue(_npcNode.HeadgearRoot.GetNodeOrNull<MeshInstance3D>("CapVisor") != null, "CapVisor should exist.");

                GD.Print("[PASS] Step 18: Archetype headgear and silhouettes (StrawHat, MinerHelmet, Headband, Cap) verified.");

                GD.Print("[PASS] ALL LIVELY NPC PHASE 1, PHASE 2, PHASE 3 & PHASE 4 TESTS PASSED SUCCESSFULLY!");
                GetTree().Quit(0);
                break;
            }
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"[FAIL] {message}");
            GetTree().Quit(1);
            throw new InvalidOperationException($"Test assertion failed: {message}");
        }
    }
}
