namespace VoxelKing.Core.Voxel;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// Maintains block-type counts and spatial coordinates for indexed block types within a single chunk.
/// </summary>
public sealed class ChunkBlockIndex
{
    private const int DefaultCapacity = 256;

    private int[] _counts;
    private readonly Dictionary<ushort, HashSet<Vec3i>> _indexedPositions = new();
    private Func<ushort, bool>? _isIndexed;
    private bool[]? _isIndexedMap;

    /// <summary>
    /// Gets the predicate used to determine if a block type should maintain a spatial position set.
    /// </summary>
    public Func<ushort, bool>? IsIndexedPredicate => _isIndexed;

    public ChunkBlockIndex(Func<ushort, bool>? isIndexed = null)
    {
        _counts = new int[DefaultCapacity];
        _counts[0] = ChunkData.Volume; // Initially all air (0)
        _isIndexed = isIndexed;
        UpdateIndexedMap();
    }

    /// <summary>
    /// Returns the total number of voxels of the given block ID in this chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCount(ushort blockId)
    {
        return blockId < _counts.Length ? _counts[blockId] : 0;
    }

    /// <summary>
    /// Returns true if this chunk contains at least one voxel of the specified block ID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasBlock(ushort blockId) => GetCount(blockId) > 0;

    /// <summary>
    /// Gets the local positions of all voxels of the specified block ID.
    /// Returns an empty collection if the block is not indexed or not present in this chunk.
    /// </summary>
    public IReadOnlyCollection<Vec3i> GetPositions(ushort blockId)
    {
        return _indexedPositions.TryGetValue(blockId, out var set)
            ? set
            : Array.Empty<Vec3i>();
    }

    /// <summary>
    /// Sets or updates the indexing predicate and rebuilds indexed positions if needed.
    /// </summary>
    public void SetIndexPredicate(Func<ushort, bool> isIndexed, ReadOnlySpan<ushort> blocks)
    {
        ArgumentNullException.ThrowIfNull(isIndexed);
        _isIndexed = isIndexed;
        UpdateIndexedMap();
        Rebuild(blocks, isIndexed);
    }

    /// <summary>
    /// Updates counts and indexed positions when a voxel changes in the chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnBlockSet(Vec3i localPos, ushort oldBlock, ushort newBlock)
    {
        if (oldBlock == newBlock)
        {
            return;
        }

        if (oldBlock < _counts.Length)
        {
            _counts[oldBlock]--;
        }

        if (newBlock >= _counts.Length)
        {
            EnsureCapacity(newBlock);
        }
        _counts[newBlock]++;

        if (_isIndexedMap != null)
        {
            if (CheckIndexed(oldBlock) && _indexedPositions.TryGetValue(oldBlock, out var oldSet))
            {
                oldSet.Remove(localPos);
            }

            if (CheckIndexed(newBlock))
            {
                if (!_indexedPositions.TryGetValue(newBlock, out var newSet))
                {
                    newSet = new HashSet<Vec3i>();
                    _indexedPositions[newBlock] = newSet;
                }
                newSet.Add(localPos);
            }
        }
    }

    /// <summary>
    /// Rebuilds all counts and indexed positions from a raw span of chunk blocks.
    /// </summary>
    public void Rebuild(ReadOnlySpan<ushort> blocks, Func<ushort, bool>? isIndexed = null)
    {
        if (isIndexed != null)
        {
            _isIndexed = isIndexed;
            UpdateIndexedMap();
        }

        Array.Clear(_counts, 0, _counts.Length);
        _indexedPositions.Clear();

        for (int i = 0; i < blocks.Length; i++)
        {
            ushort block = blocks[i];
            if (block >= _counts.Length)
            {
                EnsureCapacity(block);
            }
            _counts[block]++;

            if (CheckIndexed(block))
            {
                if (!_indexedPositions.TryGetValue(block, out var set))
                {
                    set = new HashSet<Vec3i>();
                    _indexedPositions[block] = set;
                }
                set.Add(ChunkData.GetLocalCoords(i));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckIndexed(ushort blockId)
    {
        if (_isIndexedMap != null && blockId < _isIndexedMap.Length)
        {
            return _isIndexedMap[blockId];
        }
        return _isIndexed != null && _isIndexed(blockId);
    }

    private void UpdateIndexedMap()
    {
        if (_isIndexed == null)
        {
            _isIndexedMap = null;
            return;
        }

        _isIndexedMap = new bool[DefaultCapacity];
        for (int i = 0; i < DefaultCapacity; i++)
        {
            _isIndexedMap[i] = _isIndexed((ushort)i);
        }
    }

    private void EnsureCapacity(ushort blockId)
    {
        if (blockId >= _counts.Length)
        {
            int newSize = Math.Max(blockId + 1, _counts.Length * 2);
            Array.Resize(ref _counts, newSize);
        }
    }

    /// <summary>
    /// Creates a deep copy of this chunk block index.
    /// </summary>
    public ChunkBlockIndex Clone()
    {
        var clone = new ChunkBlockIndex(_isIndexed)
        {
            _counts = (int[])_counts.Clone()
        };
        foreach (var (blockId, positions) in _indexedPositions)
        {
            clone._indexedPositions[blockId] = new HashSet<Vec3i>(positions);
        }
        return clone;
    }
}
