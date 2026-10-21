namespace VoxelKing.Core.Voxel;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Registry mapping block string identifiers to compact ushort runtime identifiers and definitions.
/// </summary>
public interface IBlockRegistry
{
    /// <summary>
    /// Gets the runtime block ID representing air (always 0).
    /// </summary>
    ushort AirId => 0;

    /// <summary>
    /// Gets the block definition for air.
    /// </summary>
    BlockDefinition Air { get; }

    /// <summary>
    /// Total number of registered block definitions.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the block definition for a numeric runtime ID, or throws KeyNotFoundException if not found.
    /// </summary>
    BlockDefinition GetDefinition(ushort id);

    /// <summary>
    /// Gets the block definition for a string ID, or throws KeyNotFoundException if not found.
    /// </summary>
    BlockDefinition GetDefinition(string stringId);

    /// <summary>
    /// Attempts to retrieve a block definition by its runtime numeric ID.
    /// </summary>
    bool TryGetDefinition(ushort id, [NotNullWhen(true)] out BlockDefinition? definition);

    /// <summary>
    /// Attempts to retrieve a block definition by its string ID.
    /// </summary>
    bool TryGetDefinition(string stringId, [NotNullWhen(true)] out BlockDefinition? definition);

    /// <summary>
    /// Gets the numeric ID for a string ID, or throws KeyNotFoundException if not found.
    /// </summary>
    ushort GetId(string stringId);

    /// <summary>
    /// Attempts to retrieve the numeric runtime ID for a string ID.
    /// </summary>
    bool TryGetId(string stringId, out ushort id);

    /// <summary>
    /// Gets the string ID for a numeric runtime ID, or throws KeyNotFoundException if not found.
    /// </summary>
    string GetStringId(ushort id);

    /// <summary>
    /// Attempts to retrieve the string ID for a numeric runtime ID.
    /// </summary>
    bool TryGetStringId(ushort id, [NotNullWhen(true)] out string? stringId);

    /// <summary>
    /// Checks whether the specified numeric runtime ID is registered.
    /// </summary>
    bool Contains(ushort id);

    /// <summary>
    /// Checks whether the specified string ID is registered.
    /// </summary>
    bool Contains(string stringId);

    /// <summary>
    /// Gets all registered block definitions ordered by numeric runtime ID.
    /// </summary>
    IReadOnlyList<BlockDefinition> AllDefinitions { get; }
}
