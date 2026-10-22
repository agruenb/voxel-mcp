namespace VoxelKing.Core.Npc.Actions;

using System.Collections.Generic;

/// <summary>
/// Closed set of standard failure reason strings specified by Lua Brain API §2.
/// </summary>
public static class ActionFailureReason
{
    public const string Unreachable = "unreachable";
    public const string Stuck = "stuck";
    public const string TooFar = "too_far";
    public const string NoItem = "no_item";
    public const string InventoryFull = "inventory_full";
    public const string InvalidTarget = "invalid_target";
    public const string TargetGone = "target_gone";
    public const string Occupied = "occupied";
    public const string Interrupted = "interrupted";
    public const string Timeout = "timeout";

    private static readonly HashSet<string> ValidReasons = new()
    {
        Unreachable,
        Stuck,
        TooFar,
        NoItem,
        InventoryFull,
        InvalidTarget,
        TargetGone,
        Occupied,
        Interrupted,
        Timeout
    };

    public static IReadOnlySet<string> All => ValidReasons;

    /// <summary>
    /// Checks whether the specified failure reason is part of the closed set.
    /// </summary>
    public static bool IsValid(string reason) => ValidReasons.Contains(reason);
}
