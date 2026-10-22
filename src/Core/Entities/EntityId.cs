namespace VoxelKing.Core.Entities;

using System;

/// <summary>
/// Strongly-typed identifier for entities in the world simulation.
/// </summary>
public readonly record struct EntityId(int Value) : IComparable<EntityId>, IFormattable
{
    public static readonly EntityId None = new(0);

    public bool IsValid => Value > 0;

    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    public static implicit operator int(EntityId id) => id.Value;
    public static explicit operator EntityId(int value) => new(value);
}
