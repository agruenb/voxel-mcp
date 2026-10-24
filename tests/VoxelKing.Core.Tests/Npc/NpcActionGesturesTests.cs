namespace VoxelKing.Core.Tests.Npc;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcActionGesturesTests
{
    [Theory]
    [InlineData("Mine", null, true)]
    [InlineData("Place", null, true)]
    [InlineData("WalkAndMine", null, true)]
    [InlineData("Attack", null, true)]
    [InlineData(null, "dig a trench", true)]
    [InlineData(null, "mine 10 iron", true)]
    [InlineData(null, "place cobblestone wall", true)]
    [InlineData(null, "build shelter", true)]
    [InlineData("GoTo", "wander around", false)]
    [InlineData("Wait", null, false)]
    [InlineData("Say", "greet player", false)]
    public void IsSwingAction_DetectsSwingingActionsAndTasks(string? action, string? task, bool expected)
    {
        bool result = NpcActionGestures.IsSwingAction(action, task);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveGestureState_SwingingTakesPrecedenceOverExpressions()
    {
        var state = NpcActionGestures.ResolveGestureState(
            isExplicitSwing: true,
            isAirborne: false,
            expression: NpcExpressionState.Thinking,
            actionName: "Mine",
            taskSummary: "mining");

        Assert.Equal(NpcGestureState.Swinging, state);
    }

    [Fact]
    public void ResolveGestureState_ThinkingExpressionMapsToThinkingPose()
    {
        var state = NpcActionGestures.ResolveGestureState(
            isExplicitSwing: false,
            isAirborne: false,
            expression: NpcExpressionState.Thinking,
            actionName: null,
            taskSummary: null);

        Assert.Equal(NpcGestureState.ThinkingPose, state);
    }

    [Fact]
    public void ResolveGestureState_TalkingExpressionMapsToTalkingGesticulation()
    {
        var state = NpcActionGestures.ResolveGestureState(
            isExplicitSwing: false,
            isAirborne: false,
            expression: NpcExpressionState.Talking,
            actionName: null,
            taskSummary: null);

        Assert.Equal(NpcGestureState.TalkingGesticulation, state);
    }

    [Fact]
    public void ResolveGestureState_AirborneTakesPrecedenceOverConversationExpressions()
    {
        var thinkAirState = NpcActionGestures.ResolveGestureState(
            isExplicitSwing: false,
            isAirborne: true,
            expression: NpcExpressionState.Thinking,
            actionName: null,
            taskSummary: null);

        var talkAirState = NpcActionGestures.ResolveGestureState(
            isExplicitSwing: false,
            isAirborne: true,
            expression: NpcExpressionState.Talking,
            actionName: null,
            taskSummary: null);

        Assert.Equal(NpcGestureState.Airborne, thinkAirState);
        Assert.Equal(NpcGestureState.Airborne, talkAirState);
    }

    [Fact]
    public void CalculateSwingAngle_OscillatesWithinHammerSwingRange()
    {
        for (float t = 0f; t < 1.0f; t += 0.05f)
        {
            float angle = NpcActionGestures.CalculateSwingAngle(t);
            // Arm should oscillate between -1.35 rad (extended strike) and -0.25 rad (cocked back)
            Assert.InRange(angle, -1.36f, -0.24f);
        }
    }

    [Fact]
    public void CalculateThinkingPoseAngles_RaisesRightArmToHead()
    {
        var pose = NpcActionGestures.CalculateThinkingPoseAngles(0.5f);

        // Raised up to temple (RotX < -2.0) and tilted inward (RotZ < -0.5)
        Assert.True(pose.RightArmRotX < -2.0f, $"RightArmRotX ({pose.RightArmRotX}) should be raised up near head.");
        Assert.True(pose.RightArmRotZ < -0.5f, $"RightArmRotZ ({pose.RightArmRotZ}) should be tilted inward toward head.");
    }

    [Fact]
    public void ResolveHeldItem_MiningAction_ReturnsPickaxe()
    {
        var inventory = new Inventory(10);
        inventory.SetSlot(0, new ItemStack("dirt", 5));

        string item = NpcActionGestures.ResolveHeldItem(inventory, actionName: "Mine", taskSummary: null);
        Assert.Equal("pickaxe", item);
    }

    [Fact]
    public void ResolveHeldItem_InventorySlot0_ReturnsSlot0ItemId()
    {
        var inventory = new Inventory(10);
        inventory.SetSlot(0, new ItemStack("wood", 12));

        string item = NpcActionGestures.ResolveHeldItem(inventory, actionName: null, taskSummary: null);
        Assert.Equal("wood", item);
    }

    [Fact]
    public void ResolveHeldItem_EmptyInventory_ReturnsDefaultItem()
    {
        var inventory = new Inventory(10);

        string item = NpcActionGestures.ResolveHeldItem(inventory, actionName: null, taskSummary: null, defaultItem: "cobblestone");
        Assert.Equal("cobblestone", item);
    }
}
