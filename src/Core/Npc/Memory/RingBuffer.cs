namespace VoxelKing.Core.Npc.Memory;

using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// High-performance, fixed-capacity circular FIFO ring buffer.
/// When capacity is reached, adding elements overwrites the oldest element.
/// Enumeration yields items in chronological order (oldest first, newest last).
/// </summary>
public sealed class RingBuffer<T> : IReadOnlyList<T>
{
    private readonly T[] _buffer;
    private int _start;
    private int _count;

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _buffer = new T[capacity];
    }

    /// <summary>
    /// Adds an item to the end of the buffer. If at capacity, overwrites the oldest item.
    /// </summary>
    public void Add(T item)
    {
        if (_count < _buffer.Length)
        {
            int index = (_start + _count) % _buffer.Length;
            _buffer[index] = item;
            _count++;
        }
        else
        {
            _buffer[_start] = item;
            _start = (_start + 1) % _buffer.Length;
        }
    }

    /// <summary>
    /// Clears all items from the ring buffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _start = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the item at the specified 0-based chronological index (0 = oldest).
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range (count: {_count}).");
            }

            return _buffer[(_start + index) % _buffer.Length];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
