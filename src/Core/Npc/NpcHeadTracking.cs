namespace VoxelKing.Core.Npc;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Facial expression states for NPC rendering and animations (F-210 Phase 1).
/// </summary>
public enum NpcExpressionState
{
    /// <summary>
    /// Neutral friendly gaze.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Squinted eyes and raised eyebrow when processing LLM inference or thinking.
    /// </summary>
    Thinking,

    /// <summary>
    /// Mouth oscillating vertically while speech bubble is visible.
    /// </summary>
    Talking
}

/// <summary>
/// Result of head tracking look-at computation.
/// </summary>
/// <param name="Yaw">Clamped neck yaw angle in radians relative to body facing (negative = turn right, positive = turn left).</param>
/// <param name="Pitch">Clamped neck pitch angle in radians (positive = tilt up, negative = tilt down).</param>
/// <param name="InTrackingRange">True if the target was within distance and field-of-view limits.</param>
public readonly record struct HeadLookResult(float Yaw, float Pitch, bool InTrackingRange);

/// <summary>
/// Pure math and logic for NPC head tracking IK, eye blinking, and expression resolution.
/// Contains no Godot dependencies and is fully testable in Core.
/// </summary>
public static class NpcHeadTracking
{
    public const float MaxYawRadians = 55.0f * (MathF.PI / 180.0f);     // ±55°
    public const float MaxPitchRadians = 30.0f * (MathF.PI / 180.0f);   // ±30°
    public const float MaxViewAngleRadians = 85.0f * (MathF.PI / 180.0f); // ±85° (behind check)
    public const float DefaultMaxTrackingDistance = 4.5f;
    public const float DefaultBlinkDuration = 0.10f;
    public const float MinBlinkInterval = 2.5f;
    public const float MaxBlinkInterval = 5.0f;

    /// <summary>
    /// Calculates neck yaw and pitch rotation angles in radians relative to the NPC body
    /// to look at the given target world position, respecting anatomical rotation limits.
    /// </summary>
    public static HeadLookResult CalculateLookAngles(
        Vec3 headPosition,
        float bodyFacing,
        Vec3 targetPosition,
        float maxDistance = DefaultMaxTrackingDistance)
    {
        var delta = targetPosition - headPosition;
        float distance = delta.Length();

        if (distance > maxDistance || distance < 0.001f)
        {
            return new HeadLookResult(0f, 0f, InTrackingRange: false);
        }

        // Convert world delta into NPC local coordinate space (where local -Z is body forward, +X is body right)
        float cos = MathF.Cos(bodyFacing);
        float sin = MathF.Sin(bodyFacing);

        float localX = delta.X * cos - delta.Z * sin;
        float localY = delta.Y;
        float localZ = delta.X * sin + delta.Z * cos;

        // Yaw angle relative to forward (-Z):
        // When local -Z is forward and +X is right:
        // yaw = Atan2(-localX, -localZ)
        float yaw = MathF.Atan2(-localX, -localZ);

        // Pitch angle relative to horizontal plane (positive = looking up, negative = looking down):
        float hDist = MathF.Sqrt(localX * localX + localZ * localZ);
        float pitch = MathF.Atan2(localY, hDist);

        // If target is behind the NPC outside natural head swivel range, do not wrench neck
        if (MathF.Abs(yaw) > MaxViewAngleRadians)
        {
            return new HeadLookResult(0f, 0f, InTrackingRange: false);
        }

        // Clamp to natural human range (Yaw: ±55°, Pitch: ±30°)
        float clampedYaw = Math.Clamp(yaw, -MaxYawRadians, MaxYawRadians);
        float clampedPitch = Math.Clamp(pitch, -MaxPitchRadians, MaxPitchRadians);

        return new HeadLookResult(clampedYaw, clampedPitch, InTrackingRange: true);
    }

    /// <summary>
    /// Computes the vertical eye scale during a blink (1.0 = fully open, 0.0 = fully closed).
    /// Uses a smooth half-sine curve over the blink duration.
    /// </summary>
    public static float CalculateBlinkScale(float progress, float duration = DefaultBlinkDuration)
    {
        if (progress <= 0f || progress >= duration || duration <= 0f)
        {
            return 1.0f;
        }

        float t = progress / duration;
        // Smooth dip from 1 down to 0 at midpoint, then back up to 1
        return Math.Clamp(1.0f - MathF.Sin(t * MathF.PI), 0.0f, 1.0f);
    }

    /// <summary>
    /// Resolves the current facial expression state based on conversation and speech bubble activity.
    /// </summary>
    public static NpcExpressionState ResolveExpression(
        NpcConversationState conversationState,
        bool isSpeechBubbleVisible,
        bool isSpeechBubbleThinking)
    {
        if (isSpeechBubbleThinking || conversationState == NpcConversationState.Thinking)
        {
            return NpcExpressionState.Thinking;
        }

        if (isSpeechBubbleVisible || conversationState == NpcConversationState.Replying)
        {
            return NpcExpressionState.Talking;
        }

        return NpcExpressionState.Default;
    }
}
