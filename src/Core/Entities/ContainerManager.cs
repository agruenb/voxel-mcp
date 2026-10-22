namespace VoxelKing.Core.Entities;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VoxelKing.Core.Common;

/// <summary>
/// Manages container inventories (such as chests) associated with world voxel block positions.
/// </summary>
public sealed class ContainerManager
{
    private readonly Dictionary<Vec3i, Inventory> _containers = new();

    public int Count => _containers.Count;

    public Inventory GetOrCreateContainer(Vec3i pos, int slotCount = 27)
    {
        if (!_containers.TryGetValue(pos, out var inv))
        {
            inv = new Inventory(slotCount);
            _containers[pos] = inv;
        }
        return inv;
    }

    public bool HasContainer(Vec3i pos) => _containers.ContainsKey(pos);

    public bool TryGetContainer(Vec3i pos, [NotNullWhen(true)] out Inventory? container)
    {
        return _containers.TryGetValue(pos, out container);
    }

    public Inventory? GetContainer(Vec3i pos)
    {
        return _containers.GetValueOrDefault(pos);
    }

    public bool RemoveContainer(Vec3i pos, [NotNullWhen(true)] out Inventory? removed)
    {
        return _containers.Remove(pos, out removed);
    }

    public void Clear() => _containers.Clear();

    public IReadOnlyDictionary<Vec3i, Inventory> AllContainers => _containers;
}
