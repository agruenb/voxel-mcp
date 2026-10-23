namespace VoxelKing.Core.Entities;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using VoxelKing.Core.Common;

/// <summary>
/// Maintains active entities in the simulation with a spatial hash grid for fast radius and nearest queries.
/// </summary>
public sealed class EntityRegistry
{
    public const float DefaultCellSize = 16.0f;

    private readonly float _cellSize;
    private readonly Dictionary<EntityId, IEntity> _entitiesById = new();
    private readonly Dictionary<EntityId, Vec3i> _entityCells = new();
    private readonly Dictionary<Vec3i, HashSet<IEntity>> _spatialGrid = new();
    private int _nextId;

    /// <summary>
    /// Gets the cell size in meters used for spatial partitioning.
    /// </summary>
    public float CellSize => _cellSize;

    /// <summary>
    /// Gets the total number of registered entities.
    /// </summary>
    public int Count => _entitiesById.Count;

    public EntityRegistry(float cellSize = DefaultCellSize)
    {
        if (cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
        }

        _cellSize = cellSize;
    }

    /// <summary>
    /// Ensures that the internal ID counter is at least the specified minimum value.
    /// </summary>
    public void EnsureNextIdAtLeast(int minId)
    {
        while (true)
        {
            int current = Volatile.Read(ref _nextId);
            if (current >= minId)
            {
                break;
            }
            if (Interlocked.CompareExchange(ref _nextId, minId, current) == current)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Generates a monotonically increasing, unique entity ID starting from 1.
    /// </summary>
    public EntityId GenerateNextId()
    {
        while (true)
        {
            var id = new EntityId(Interlocked.Increment(ref _nextId));
            if (!_entitiesById.ContainsKey(id))
            {
                return id;
            }
        }
    }

    /// <summary>
    /// Registers an entity and inserts it into the spatial hash grid.
    /// </summary>
    public void Register(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!entity.Id.IsValid)
        {
            throw new ArgumentException("Entity ID must be valid (> 0).", nameof(entity));
        }

        if (_entitiesById.ContainsKey(entity.Id))
        {
            throw new InvalidOperationException($"Entity with ID {entity.Id} is already registered.");
        }

        _entitiesById[entity.Id] = entity;
        var cell = GetCellCoord(entity.Position);
        _entityCells[entity.Id] = cell;
        AddToCell(cell, entity);
    }

    /// <summary>
    /// Unregisters an entity by ID and removes it from the spatial hash grid.
    /// </summary>
    public bool Unregister(EntityId id)
    {
        if (!_entitiesById.Remove(id, out var entity))
        {
            return false;
        }

        if (_entityCells.Remove(id, out var cell))
        {
            RemoveFromCell(cell, entity);
        }

        return true;
    }

    /// <summary>
    /// Updates the position of an entity and migrates it between spatial cells if it crossed a cell boundary.
    /// </summary>
    public void UpdatePosition(IEntity entity, Vec3 newPosition)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Position = newPosition;
        UpdateSpatial(entity);
    }

    /// <summary>
    /// Synchronizes an entity's position in the spatial grid with its current Position property.
    /// </summary>
    public void UpdateSpatial(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!_entitiesById.ContainsKey(entity.Id))
        {
            return;
        }

        var newCell = GetCellCoord(entity.Position);
        if (_entityCells.TryGetValue(entity.Id, out var oldCell))
        {
            if (oldCell == newCell)
            {
                return;
            }

            RemoveFromCell(oldCell, entity);
        }

        _entityCells[entity.Id] = newCell;
        AddToCell(newCell, entity);
    }

    /// <summary>
    /// Retrieves an entity by its ID, or throws KeyNotFoundException if not found.
    /// </summary>
    public IEntity Get(EntityId id)
    {
        if (_entitiesById.TryGetValue(id, out var entity))
        {
            return entity;
        }

        throw new KeyNotFoundException($"Entity with ID {id} was not found.");
    }

    /// <summary>
    /// Retrieves an entity by its ID and casts to the specified entity type.
    /// </summary>
    public T Get<T>(EntityId id) where T : class, IEntity
    {
        var entity = Get(id);
        if (entity is T typed)
        {
            return typed;
        }

        throw new InvalidCastException($"Entity {id} is {entity.GetType().Name}, not {typeof(T).Name}.");
    }

    /// <summary>
    /// Attempts to retrieve an entity by its ID.
    /// </summary>
    public bool TryGet(EntityId id, [NotNullWhen(true)] out IEntity? entity)
    {
        return _entitiesById.TryGetValue(id, out entity);
    }

    /// <summary>
    /// Attempts to retrieve an entity by its ID and cast to the specified entity type.
    /// </summary>
    public bool TryGet<T>(EntityId id, [NotNullWhen(true)] out T? entity) where T : class, IEntity
    {
        if (_entitiesById.TryGetValue(id, out var raw) && raw is T typed)
        {
            entity = typed;
            return true;
        }

        entity = null;
        return false;
    }

    /// <summary>
    /// Returns all registered entities.
    /// </summary>
    public IReadOnlyCollection<IEntity> GetAll() => _entitiesById.Values;

    /// <summary>
    /// Returns all registered entities of the specified kind.
    /// </summary>
    public IEnumerable<IEntity> GetAll(EntityKind kind)
    {
        foreach (var entity in _entitiesById.Values)
        {
            if (entity.Kind == kind)
            {
                yield return entity;
            }
        }
    }

    /// <summary>
    /// Returns all registered entities of the specified concrete type.
    /// </summary>
    public IEnumerable<T> GetAll<T>() where T : class, IEntity
    {
        foreach (var entity in _entitiesById.Values)
        {
            if (entity is T typed)
            {
                yield return typed;
            }
        }
    }

    /// <summary>
    /// Finds the nearest active entity to the origin within radius, optionally filtering by kind.
    /// Returns null if no matching entity is within radius.
    /// </summary>
    public IEntity? FindNearest(Vec3 origin, float radius, EntityKind? kind = null)
    {
        if (radius <= 0f)
        {
            return null;
        }

        float radiusSq = radius * radius;
        float bestDistSq = float.MaxValue;
        IEntity? bestEntity = null;

        int minX = (int)MathF.Floor((origin.X - radius) / _cellSize);
        int maxX = (int)MathF.Floor((origin.X + radius) / _cellSize);
        int minY = (int)MathF.Floor((origin.Y - radius) / _cellSize);
        int maxY = (int)MathF.Floor((origin.Y + radius) / _cellSize);
        int minZ = (int)MathF.Floor((origin.Z - radius) / _cellSize);
        int maxZ = (int)MathF.Floor((origin.Z + radius) / _cellSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var cellCoord = new Vec3i(x, y, z);
                    if (!_spatialGrid.TryGetValue(cellCoord, out var cellEntities))
                    {
                        continue;
                    }

                    foreach (var entity in cellEntities)
                    {
                        if (kind.HasValue && entity.Kind != kind.Value)
                        {
                            continue;
                        }

                        if (!entity.IsActive)
                        {
                            continue;
                        }

                        float distSq = origin.DistanceSquared(entity.Position);
                        if (distSq <= radiusSq && distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestEntity = entity;
                        }
                    }
                }
            }
        }

        return bestEntity;
    }

    /// <summary>
    /// Finds all active entities within radius, ordered nearest-first, up to the maximum specified count.
    /// </summary>
    public IReadOnlyList<IEntity> FindAll(Vec3 origin, float radius, EntityKind? kind = null, int max = int.MaxValue)
    {
        if (radius <= 0f || max <= 0)
        {
            return Array.Empty<IEntity>();
        }

        float radiusSq = radius * radius;
        var candidates = new List<(IEntity Entity, float DistSq)>();

        int minX = (int)MathF.Floor((origin.X - radius) / _cellSize);
        int maxX = (int)MathF.Floor((origin.X + radius) / _cellSize);
        int minY = (int)MathF.Floor((origin.Y - radius) / _cellSize);
        int maxY = (int)MathF.Floor((origin.Y + radius) / _cellSize);
        int minZ = (int)MathF.Floor((origin.Z - radius) / _cellSize);
        int maxZ = (int)MathF.Floor((origin.Z + radius) / _cellSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var cellCoord = new Vec3i(x, y, z);
                    if (!_spatialGrid.TryGetValue(cellCoord, out var cellEntities))
                    {
                        continue;
                    }

                    foreach (var entity in cellEntities)
                    {
                        if (kind.HasValue && entity.Kind != kind.Value)
                        {
                            continue;
                        }

                        if (!entity.IsActive)
                        {
                            continue;
                        }

                        float distSq = origin.DistanceSquared(entity.Position);
                        if (distSq <= radiusSq)
                        {
                            candidates.Add((entity, distSq));
                        }
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<IEntity>();
        }

        candidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));

        int resultCount = Math.Min(candidates.Count, max);
        var result = new IEntity[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            result[i] = candidates[i].Entity;
        }

        return result;
    }

    /// <summary>
    /// Clears all entities and resets the spatial grid.
    /// </summary>
    public void Clear()
    {
        _entitiesById.Clear();
        _entityCells.Clear();
        _spatialGrid.Clear();
    }

    /// <summary>
    /// Computes the integer cell coordinate for a continuous 3D world position.
    /// </summary>
    public Vec3i GetCellCoord(Vec3 pos)
    {
        int cx = (int)MathF.Floor(pos.X / _cellSize);
        int cy = (int)MathF.Floor(pos.Y / _cellSize);
        int cz = (int)MathF.Floor(pos.Z / _cellSize);
        return new Vec3i(cx, cy, cz);
    }

    private void AddToCell(Vec3i cell, IEntity entity)
    {
        if (!_spatialGrid.TryGetValue(cell, out var set))
        {
            set = new HashSet<IEntity>();
            _spatialGrid[cell] = set;
        }

        set.Add(entity);
    }

    private void RemoveFromCell(Vec3i cell, IEntity entity)
    {
        if (_spatialGrid.TryGetValue(cell, out var set))
        {
            set.Remove(entity);
            if (set.Count == 0)
            {
                _spatialGrid.Remove(cell);
            }
        }
    }
}
