namespace VoxelKing.Core.Brain;

using System.Collections.Generic;

/// <summary>
/// Data representing a requested blocking action initiated by an NPC brain coroutine.
/// </summary>
public sealed record ActionRequest(
    string ActionName,
    IReadOnlyList<object?> Arguments,
    float RequestTime = 0f);
