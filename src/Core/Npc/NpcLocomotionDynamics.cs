namespace VoxelKing.Core.Npc;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Target joint rotations for airborne jumping and falling poses (F-210 Phase 2).
/// </summary>
public readonly record struct AirborneJointAngles(
    float LeftArmRotX, float LeftArmRotZ,
    float RightArmRotX, float RightArmRotZ,
    float LeftLegRotX, float RightLegRotX);

/// <summary>
/// Pure math and calculation for secondary locomotion dynamics (F-210 Phase 2):
/// Turn banking, forward movement lean, airborne jump/fall posing, and landing squash/stretch.
/// Contains no Godot dependencies and is fully testable in Core.
/// </summary>
public static class NpcLocomotionDynamics
{
    public const float MaxBankRadians = 10.0f * (MathF.PI / 180.0f);        // ~10° (0.1745 rad)
    public const float MaxForwardLeanRadians = 6.0f * (MathF.PI / 180.0f);  // ~6° (0.1047 rad)
    public const float DefaultLandingDuration = 0.15f;                     // 150ms
    public const float DefaultMaxSquash = 0.08f;                           // 8% vertical compression

    /// <summary>
    /// Calculates torso roll lean angle (banking) into a turn from angular turning velocity.
    /// Negative angular velocity (turning right) produces negative roll (leaning right).
    /// Positive angular velocity (turning left) produces positive roll (leaning left).
    /// </summary>
    public static float CalculateTurnBank(float angularVelocity, float maxBank = MaxBankRadians)
    {
        // 0.20 scaling factor gives ~6° bank at typical turning speed (~0.5 rad/s)
        float bank = angularVelocity * 0.20f;
        return Math.Clamp(bank, -maxBank, maxBank);
    }

    /// <summary>
    /// Calculates forward torso pitch lean based on horizontal forward speed.
    /// Pitch is negative in Godot coordinate space to lean forward towards -Z.
    /// </summary>
    public static float CalculateForwardLean(float forwardSpeed, float maxLean = MaxForwardLeanRadians)
    {
        if (forwardSpeed <= 0.1f)
        {
            return 0f;
        }

        float lean = -MathF.Min(forwardSpeed * 0.015f, maxLean);
        return lean;
    }

    /// <summary>
    /// Computes the squash and stretch scale vector (X, Y, Z) during landing impact.
    /// At impact, Y compresses (0.92) while X and Z expand (1.08), bouncing back to (1, 1, 1) over duration.
    /// </summary>
    public static Vec3 CalculateLandingSquash(float progress, float duration = DefaultLandingDuration, float maxSquash = DefaultMaxSquash)
    {
        if (progress < 0f || progress >= duration || duration <= 0f)
        {
            return Vec3.One;
        }

        float t = progress / duration; // 0.0 to 1.0
        // Damped sine bounce curve: peaks at start and decays smoothly to 0 at t = 1.0
        float bounce = MathF.Sin((1.0f - t) * MathF.PI * 0.5f) * (1.0f - t);
        float squashFactor = bounce * maxSquash;

        float scaleY = 1.0f - squashFactor;
        float scaleXZ = 1.0f + squashFactor;

        return new Vec3(scaleXZ, scaleY, scaleXZ);
    }

    /// <summary>
    /// Resolves target arm and leg angles for airborne ascending (jumping) vs descending (falling) states.
    /// </summary>
    public static AirborneJointAngles CalculateAirbornePose(float verticalVelocity)
    {
        if (verticalVelocity > 0.3f)
        {
            // Ascending (jumping): arms raised forward slightly, legs extended downwards
            return new AirborneJointAngles(
                LeftArmRotX: -0.45f, LeftArmRotZ: -0.15f,
                RightArmRotX: -0.45f, RightArmRotZ: 0.15f,
                LeftLegRotX: 0.15f, RightLegRotX: -0.10f);
        }
        else if (verticalVelocity < -0.3f)
        {
            // Falling: arms flare outwards for balance, legs spread slightly
            return new AirborneJointAngles(
                LeftArmRotX: -0.30f, LeftArmRotZ: -0.40f,
                RightArmRotX: -0.30f, RightArmRotZ: 0.40f,
                LeftLegRotX: -0.15f, RightLegRotX: 0.20f);
        }
        else
        {
            // Peak/apex of jump
            return new AirborneJointAngles(
                LeftArmRotX: -0.35f, LeftArmRotZ: -0.25f,
                RightArmRotX: -0.35f, RightArmRotZ: 0.25f,
                LeftLegRotX: 0.0f, RightLegRotX: 0.0f);
        }
    }
}
