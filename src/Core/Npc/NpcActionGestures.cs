namespace VoxelKing.Core.Npc;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Contextual action gesture states for NPC upper-body posing and arm animations (F-210 Phase 3).
/// </summary>
public enum NpcGestureState
{
    None = 0,

    /// <summary>
    /// Rapid hammer/tool swing of the right arm while digging, mining, or placing blocks.
    /// </summary>
    Swinging,

    /// <summary>
    /// Right arm raised scratching the temple while LLM inference is running or NPC is thinking.
    /// </summary>
    ThinkingPose,

    /// <summary>
    /// Subtle hand gesticulation while speaking dialogue.
    /// </summary>
    TalkingGesticulation,

    /// <summary>
    /// Airborne arm flaring or jumping pose.
    /// </summary>
    Airborne
}

/// <summary>
/// Joint rotation angles for arm gesticulation during speech.
/// </summary>
public readonly record struct TalkGesticulationAngles(
    float RightArmRotX, float RightArmRotZ,
    float LeftArmRotX, float LeftArmRotZ);

/// <summary>
/// Joint rotation angles for the head-scratching thinking pose.
/// </summary>
public readonly record struct ThinkingPoseAngles(
    float RightArmRotX, float RightArmRotY, float RightArmRotZ);

/// <summary>
/// Pure math and calculation for NPC action gestures and handheld items (F-210 Phase 3).
/// Free of any Godot dependencies and fully testable in Core.
/// </summary>
public static class NpcActionGestures
{
    /// <summary>
    /// Determines whether the given action name or task summary represents a swinging activity
    /// (mining, digging, placing, building, cutting, attacking).
    /// </summary>
    public static bool IsSwingAction(string? actionName, string? taskSummary)
    {
        if (!string.IsNullOrEmpty(actionName))
        {
            if (actionName.Contains("Mine", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Place", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Attack", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Dig", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(taskSummary))
        {
            if (taskSummary.Contains("mine", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("dig", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("place", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("build", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("quarry", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("chop", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("harvest", StringComparison.OrdinalIgnoreCase) ||
                taskSummary.Contains("attack", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the current active gesture state based on explicit flags, actions, expressions, and grounded state.
    /// </summary>
    public static NpcGestureState ResolveGestureState(
        bool isExplicitSwing,
        bool isAirborne,
        NpcExpressionState expression,
        string? actionName,
        string? taskSummary)
    {
        // 1. Digging / Mining / Placing swing gesture takes highest priority
        if (isExplicitSwing || IsSwingAction(actionName, taskSummary))
        {
            return NpcGestureState.Swinging;
        }

        // 2. Airborne jump/fall dynamics take precedence over idle conversation gestures
        if (isAirborne)
        {
            return NpcGestureState.Airborne;
        }

        // 3. Thinking head-scratch pose
        if (expression == NpcExpressionState.Thinking)
        {
            return NpcGestureState.ThinkingPose;
        }

        // 4. Conversation hand gesticulation
        if (expression == NpcExpressionState.Talking)
        {
            return NpcGestureState.TalkingGesticulation;
        }

        return NpcGestureState.None;
    }

    /// <summary>
    /// Computes the right arm pitch angle for a rapid forward-and-back hammer swing cycle.
    /// Range: -0.25 rad (cocked back) to -1.35 rad (extended strike forward).
    /// </summary>
    public static float CalculateSwingAngle(float time, float frequency = 18.0f)
    {
        float cycle = MathF.Sin(time * frequency);
        return -0.8f + cycle * 0.55f;
    }

    /// <summary>
    /// Computes joint angles for the right arm raised to the temple scratching the head in thought.
    /// </summary>
    public static ThinkingPoseAngles CalculateThinkingPoseAngles(float time)
    {
        float scratch = MathF.Sin(time * 10.0f);
        return new ThinkingPoseAngles(
            RightArmRotX: -2.25f + scratch * 0.12f,
            RightArmRotY: 0.20f,
            RightArmRotZ: -0.65f + scratch * 0.06f);
    }

    /// <summary>
    /// Computes subtle conversation gesticulation angles for both arms while speaking.
    /// </summary>
    public static TalkGesticulationAngles CalculateTalkingGesticulationAngles(float time)
    {
        float t = time * 3.5f;
        float rightX = -0.35f + MathF.Sin(t) * 0.15f;
        float rightZ = 0.20f + MathF.Cos(t * 0.8f) * 0.10f;
        float leftX = -0.25f + MathF.Cos(t * 1.2f) * 0.10f;
        float leftZ = -0.15f + MathF.Sin(t * 0.7f) * 0.08f;

        return new TalkGesticulationAngles(rightX, rightZ, leftX, leftZ);
    }

    /// <summary>
    /// Resolves which item or tool should be rendered in the NPC's hand:
    /// - If mining or task says "mine"/"dig": returns "pickaxe".
    /// - If placing or task says "place": returns the placed block ID or inventory block.
    /// - If inventory has an item in slot 0 (or first slot): returns that item.
    /// - Otherwise falls back to defaultItem.
    /// </summary>
    public static string ResolveHeldItem(
        IInventory? inventory,
        string? actionName,
        string? taskSummary,
        string defaultItem = "cobblestone")
    {
        // If mining activity, hold a pickaxe tool
        if ((actionName != null && actionName.Contains("Mine", StringComparison.OrdinalIgnoreCase)) ||
            (taskSummary != null && (taskSummary.Contains("mine", StringComparison.OrdinalIgnoreCase) || taskSummary.Contains("dig", StringComparison.OrdinalIgnoreCase))))
        {
            return "pickaxe";
        }

        // If inventory has items, check slot 0 or first non-empty slot
        if (inventory != null)
        {
            var slot0 = inventory.GetSlot(0);
            if (slot0 != null && !string.IsNullOrWhiteSpace(slot0.ItemId))
            {
                return slot0.ItemId;
            }

            for (int i = 1; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !string.IsNullOrWhiteSpace(slot.ItemId))
                {
                    return slot.ItemId;
                }
            }
        }

        return defaultItem;
    }
}
