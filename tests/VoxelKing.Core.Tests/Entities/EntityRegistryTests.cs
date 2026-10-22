namespace VoxelKing.Core.Tests.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using Xunit;

public class EntityRegistryTests
{
    private sealed class TestEntity : IEntity
    {
        public EntityId Id { get; init; }
        public EntityKind Kind { get; init; }
        public string Name { get; init; }
        public Vec3 Position { get; set; }
        public Vec3 Velocity { get; set; }
        public bool IsActive { get; set; } = true;

        public TestEntity(EntityId id, EntityKind kind, string name, Vec3 position)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Position = position;
            Velocity = Vec3.Zero;
        }
    }

    [Fact]
    public void Register_And_Get_Entity_Succeeds()
    {
        var registry = new EntityRegistry(16.0f);
        var id = registry.GenerateNextId();
        var entity = new TestEntity(id, EntityKind.Npc, "Bob", new Vec3(10f, 0f, 5f));

        registry.Register(entity);

        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGet(id, out var retrieved));
        Assert.Same(entity, retrieved);
        Assert.Same(entity, registry.Get(id));
    }

    [Fact]
    public void Unregister_Removes_Entity_From_Registry_And_Spatial_Grid()
    {
        var registry = new EntityRegistry(16.0f);
        var id = registry.GenerateNextId();
        var entity = new TestEntity(id, EntityKind.Npc, "Bob", new Vec3(5f, 0f, 5f));

        registry.Register(entity);
        Assert.NotNull(registry.FindNearest(new Vec3(5f, 0f, 5f), 10f));

        bool removed = registry.Unregister(id);

        Assert.True(removed);
        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(id, out _));
        Assert.Null(registry.FindNearest(new Vec3(5f, 0f, 5f), 10f));
    }

    [Fact]
    public void UpdatePosition_Migrates_Entity_Between_Cells()
    {
        var registry = new EntityRegistry(16.0f); // Cell bounds: [0..16), [16..32), etc.
        var id = registry.GenerateNextId();
        var entity = new TestEntity(id, EntityKind.Item, "StoneItem", new Vec3(2f, 0f, 2f));

        registry.Register(entity);

        // Move to a distant cell
        registry.UpdatePosition(entity, new Vec3(100f, 0f, 100f));

        // Original location should have no entities within small radius
        Assert.Null(registry.FindNearest(new Vec3(2f, 0f, 2f), 10f));

        // New location should find the entity
        var found = registry.FindNearest(new Vec3(100f, 0f, 100f), 10f);
        Assert.NotNull(found);
        Assert.Equal(id, found.Id);
    }

    [Fact]
    public void FindNearest_Returns_Closest_Matching_Entity()
    {
        var registry = new EntityRegistry(16.0f);
        var origin = new Vec3(0f, 0f, 0f);

        var e1 = new TestEntity(registry.GenerateNextId(), EntityKind.Npc, "Close", new Vec3(3f, 0f, 4f)); // Dist = 5
        var e2 = new TestEntity(registry.GenerateNextId(), EntityKind.Npc, "Medium", new Vec3(6f, 0f, 8f)); // Dist = 10
        var e3 = new TestEntity(registry.GenerateNextId(), EntityKind.Player, "PlayerClose", new Vec3(2f, 0f, 0f)); // Dist = 2

        registry.Register(e1);
        registry.Register(e2);
        registry.Register(e3);

        // Without kind filter -> Player is closest (dist 2)
        var nearestAny = registry.FindNearest(origin, 15f);
        Assert.NotNull(nearestAny);
        Assert.Equal(e3.Id, nearestAny.Id);

        // With kind filter: Npc -> Close NPC is closest (dist 5)
        var nearestNpc = registry.FindNearest(origin, 15f, EntityKind.Npc);
        Assert.NotNull(nearestNpc);
        Assert.Equal(e1.Id, nearestNpc.Id);

        // Radius too small -> returns null
        Assert.Null(registry.FindNearest(origin, 1.0f));
    }

    [Fact]
    public void FindAll_Returns_Entities_Sorted_Nearest_First_And_Respects_Max()
    {
        var registry = new EntityRegistry(16.0f);
        var origin = new Vec3(0f, 0f, 0f);

        var e1 = new TestEntity(registry.GenerateNextId(), EntityKind.Item, "Item1", new Vec3(10f, 0f, 0f));
        var e2 = new TestEntity(registry.GenerateNextId(), EntityKind.Item, "Item2", new Vec3(2f, 0f, 0f));
        var e3 = new TestEntity(registry.GenerateNextId(), EntityKind.Item, "Item3", new Vec3(5f, 0f, 0f));
        var e4 = new TestEntity(registry.GenerateNextId(), EntityKind.Npc, "Npc1", new Vec3(3f, 0f, 0f));

        registry.Register(e1);
        registry.Register(e2);
        registry.Register(e3);
        registry.Register(e4);

        // Find all items within radius 20
        var items = registry.FindAll(origin, 20f, EntityKind.Item);

        Assert.Equal(3, items.Count);
        Assert.Equal(e2.Id, items[0].Id); // Dist 2
        Assert.Equal(e3.Id, items[1].Id); // Dist 5
        Assert.Equal(e1.Id, items[2].Id); // Dist 10

        // Respect max count
        var top2 = registry.FindAll(origin, 20f, EntityKind.Item, max: 2);
        Assert.Equal(2, top2.Count);
        Assert.Equal(e2.Id, top2[0].Id);
        Assert.Equal(e3.Id, top2[1].Id);
    }

    [Fact]
    public void SpatialGrid_Matches_BruteForce_Over_200_Random_Entities()
    {
        var registry = new EntityRegistry(16.0f);
        var rng = new Random(42);
        var allEntities = new List<TestEntity>();

        for (int i = 0; i < 200; i++)
        {
            var id = registry.GenerateNextId();
            var kind = (EntityKind)(i % 4);
            var pos = new Vec3(
                (float)(rng.NextDouble() * 200.0 - 100.0),
                (float)(rng.NextDouble() * 60.0 - 30.0),
                (float)(rng.NextDouble() * 200.0 - 100.0)
            );
            var entity = new TestEntity(id, kind, $"Entity_{i}", pos);
            registry.Register(entity);
            allEntities.Add(entity);
        }

        // Test multiple query points and radii
        var queryPoints = new[]
        {
            new Vec3(0f, 0f, 0f),
            new Vec3(50f, 10f, -40f),
            new Vec3(-70f, -5f, 80f),
            new Vec3(25f, 0f, 25f)
        };

        float[] radii = { 10f, 25f, 50f, 75f };

        foreach (var queryPos in queryPoints)
        {
            foreach (var radius in radii)
            {
                foreach (EntityKind? kind in new EntityKind?[] { null, EntityKind.Player, EntityKind.Npc, EntityKind.Item })
                {
                    // Spatial grid result
                    var gridResult = registry.FindAll(queryPos, radius, kind);

                    // Brute force result
                    var bruteResult = allEntities
                        .Where(e => (!kind.HasValue || e.Kind == kind.Value) && e.Position.Distance(queryPos) <= radius)
                        .OrderBy(e => e.Position.DistanceSquared(queryPos))
                        .ToList();

                    Assert.Equal(bruteResult.Count, gridResult.Count);
                    for (int k = 0; k < bruteResult.Count; k++)
                    {
                        Assert.Equal(bruteResult[k].Id, gridResult[k].Id);
                    }

                    // Test FindNearest against brute force
                    var gridNearest = registry.FindNearest(queryPos, radius, kind);
                    var bruteNearest = bruteResult.FirstOrDefault();

                    if (bruteNearest == null)
                    {
                        Assert.Null(gridNearest);
                    }
                    else
                    {
                        Assert.NotNull(gridNearest);
                        Assert.Equal(bruteNearest.Id, gridNearest.Id);
                    }
                }
            }
        }
    }
}
