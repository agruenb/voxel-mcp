namespace VoxelKing.Core.Entities;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;

/// <summary>
/// Registry of named world locations accessible to player interaction and NPC memory.
/// </summary>
public sealed class NamedPlaces
{
    private readonly Dictionary<string, Vec3i> _places = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _places.Count;

    public void SetPlace(string name, Vec3i pos)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Place name cannot be null or empty", nameof(name));
        }

        _places[name] = pos;
    }

    public Vec3i? GetPlace(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _places.TryGetValue(name, out var pos) ? pos : null;
    }

    public bool HasPlace(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _places.ContainsKey(name);
    }

    public bool RemovePlace(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _places.Remove(name);
    }

    public void Clear() => _places.Clear();

    public IReadOnlyDictionary<string, Vec3i> AllPlaces => _places;
}
