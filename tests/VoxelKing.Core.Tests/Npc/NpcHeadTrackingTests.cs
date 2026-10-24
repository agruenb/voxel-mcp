namespace VoxelKing.Core.Tests.Npc;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcHeadTrackingTests
{
    private const float DegToRad = MathF.PI / 180f;

    [Fact]
    public void TargetStraightAhead_ReturnsZeroYawAndPitch()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(0f, 1.55f, -3.0f);

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.True(result.InTrackingRange);
        Assert.InRange(result.Yaw, -0.01f, 0.01f);
        Assert.InRange(result.Pitch, -0.01f, 0.01f);
    }

    [Fact]
    public void TargetToRight_ReturnsNegativeYawWithinClampedLimits()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(2f, 1.55f, -2f); // ~45 deg to the right

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.True(result.InTrackingRange);
        Assert.True(result.Yaw < 0f, "Yaw should be negative when turning right.");
        Assert.InRange(result.Yaw, -NpcHeadTracking.MaxYawRadians - 0.001f, -0.1f);
        Assert.InRange(result.Pitch, -0.01f, 0.01f);
    }

    [Fact]
    public void TargetToLeft_ReturnsPositiveYawWithinClampedLimits()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(-2f, 1.55f, -2f); // ~45 deg to the left

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.True(result.InTrackingRange);
        Assert.True(result.Yaw > 0f, "Yaw should be positive when turning left.");
        Assert.InRange(result.Yaw, 0.1f, NpcHeadTracking.MaxYawRadians + 0.001f);
        Assert.InRange(result.Pitch, -0.01f, 0.01f);
    }

    [Fact]
    public void ExtremeSideAngle_ClampsToMaxYawLimit()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(3.0f, 1.55f, -0.5f); // ~80 deg to the right

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.True(result.InTrackingRange);
        // Should clamp exactly to -MaxYawRadians (-55 deg)
        Assert.Equal(-NpcHeadTracking.MaxYawRadians, result.Yaw, precision: 4);
    }

    [Fact]
    public void TargetAbove_ClampsToMaxPitch()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(0f, 4.5f, -1.0f); // High angle up

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.True(result.InTrackingRange);
        Assert.Equal(NpcHeadTracking.MaxPitchRadians, result.Pitch, precision: 4);
    }

    [Fact]
    public void TargetBelow_ClampsToMinPitch()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(0f, -2.0f, -1.0f); // Steep angle down

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.True(result.InTrackingRange);
        Assert.Equal(-NpcHeadTracking.MaxPitchRadians, result.Pitch, precision: 4);
    }

    [Fact]
    public void TargetBeyondMaxDistance_ReturnsNotInTrackingRange()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(0f, 1.55f, -5.5f); // > 4.5m

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos, maxDistance: 4.5f);

        Assert.False(result.InTrackingRange);
        Assert.Equal(0f, result.Yaw);
        Assert.Equal(0f, result.Pitch);
    }

    [Fact]
    public void TargetBehindNpc_ReturnsNotInTrackingRange()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        var targetPos = new Vec3(0f, 1.55f, 3.0f); // Directly behind (+Z)

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing: 0f, targetPos);

        Assert.False(result.InTrackingRange, "Target behind NPC should not wrench neck beyond natural range.");
    }

    [Fact]
    public void RotatedBodyFacing_CalculatesLocalLookAnglesCorrectly()
    {
        var headPos = new Vec3(0f, 1.55f, 0f);
        // NPC is turned 90 deg left (bodyFacing = PI/2), so forward is -X
        float bodyFacing = MathF.PI / 2f;
        var targetPos = new Vec3(-3.0f, 1.55f, 0f); // Directly in front of the rotated body

        var result = NpcHeadTracking.CalculateLookAngles(headPos, bodyFacing, targetPos);

        Assert.True(result.InTrackingRange);
        Assert.InRange(result.Yaw, -0.01f, 0.01f);
        Assert.InRange(result.Pitch, -0.01f, 0.01f);
    }

    [Theory]
    [InlineData(0.00f, 1.0f)]
    [InlineData(0.05f, 0.0f)] // Midpoint of 0.10s blink is fully closed
    [InlineData(0.10f, 1.0f)] // End of blink is fully open
    [InlineData(-0.01f, 1.0f)]
    [InlineData(0.15f, 1.0f)]
    public void CalculateBlinkScale_SmoothlyDipsAtMidpoint(float progress, float expectedScale)
    {
        float scale = NpcHeadTracking.CalculateBlinkScale(progress, duration: 0.10f);
        Assert.Equal(expectedScale, scale, precision: 2);
    }

    [Theory]
    [InlineData(NpcConversationState.None, false, false, NpcExpressionState.Default)]
    [InlineData(NpcConversationState.Listening, false, false, NpcExpressionState.Default)]
    [InlineData(NpcConversationState.Thinking, false, false, NpcExpressionState.Thinking)]
    [InlineData(NpcConversationState.None, false, true, NpcExpressionState.Thinking)]
    [InlineData(NpcConversationState.Replying, false, false, NpcExpressionState.Talking)]
    [InlineData(NpcConversationState.None, true, false, NpcExpressionState.Talking)]
    public void ResolveExpression_CorrectlyMapsStates(
        NpcConversationState convState,
        bool bubbleVisible,
        bool bubbleThinking,
        NpcExpressionState expected)
    {
        var expression = NpcHeadTracking.ResolveExpression(convState, bubbleVisible, bubbleThinking);
        Assert.Equal(expected, expression);
    }
}
