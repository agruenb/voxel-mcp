namespace VoxelKing.Core.Tests.Npc.Perception;

using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Perception;
using VoxelKing.Core.Voxel;
using Xunit;

public class PerceptionServiceTests
{
    private static (VoxelWorld World, IBlockRegistry Blocks, EventBus Bus, EntityRegistry Registry, PerceptionService Perception) CreateTestEnv(int blockRadius = 24, int entityRadius = 32)
    {
        var bus = new EventBus();
        var blocks = BlockRegistry.CreateDefault();
        var world = new VoxelWorld(blocks, bus);
        var registry = new EntityRegistry();
        var perception = new PerceptionService(world, registry, blockRadius, entityRadius);
        return (world, blocks, bus, registry, perception);
    }

    [Fact]
    public void GetBlock_ReturnsStringId_WhenWithinRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(5, 1, 0);
        world.SetBlock(pos, stone);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        string? blockName = perception.GetBlock(observer, pos);

        Assert.Equal("stone", blockName);
    }

    [Fact]
    public void GetBlock_ReturnsNull_WhenBeyondRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");
        var pos = new Vec3i(30, 1, 0); // Distance ~29.5 > 24
        world.SetBlock(pos, stone);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        string? blockName = perception.GetBlock(observer, pos);

        Assert.Null(blockName);
    }

    [Fact]
    public void FindBlock_FindsNearest_WithinRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort log = blocks.GetId("oak_log");

        // Further block at (10, 1, 0)
        world.SetBlock(10, 1, 0, log);
        // Nearer block at (3, 1, 0)
        world.SetBlock(3, 1, 0, log);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        var nearest = perception.FindBlock(observer, "oak_log");

        Assert.NotNull(nearest);
        Assert.Equal(new Vec3i(3, 1, 0), nearest.Value);
    }

    [Fact]
    public void FindBlock_ReturnsNull_WhenBeyondRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort log = blocks.GetId("oak_log");

        // Only block is at (30, 1, 0), beyond radius 24
        world.SetBlock(30, 1, 0, log);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        var nearest = perception.FindBlock(observer, "oak_log");

        Assert.Null(nearest);
    }

    [Fact]
    public void FindBlock_RespectsCustomSmallerRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort log = blocks.GetId("oak_log");

        world.SetBlock(10, 1, 0, log);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        // Custom radius 5 should not find the block at 10
        var nearest = perception.FindBlock(observer, "oak_log", radius: 5);
        Assert.Null(nearest);

        // Custom radius 12 should find it
        nearest = perception.FindBlock(observer, "oak_log", radius: 12);
        Assert.NotNull(nearest);
        Assert.Equal(new Vec3i(10, 1, 0), nearest.Value);
    }

    [Fact]
    public void FindBlocks_OrdersNearestFirst_AndRespectsMax()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");

        world.SetBlock(8, 1, 0, stone);
        world.SetBlock(2, 1, 0, stone);
        world.SetBlock(15, 1, 0, stone);
        world.SetBlock(5, 1, 0, stone);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        var results = perception.FindBlocks(observer, "stone", max: 3);

        Assert.Equal(3, results.Count);
        Assert.Equal(new Vec3i(2, 1, 0), results[0]);
        Assert.Equal(new Vec3i(5, 1, 0), results[1]);
        Assert.Equal(new Vec3i(8, 1, 0), results[2]);
    }

    [Fact]
    public void FindBlocks_ClampsMaxTo64()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");

        // Place 70 blocks along a line
        for (int z = 0; z < 70; z++)
        {
            world.SetBlock(0, 1, z, stone);
        }

        var observer = new Vec3(0.5f, 1.0f, 0.5f);
        var results = perception.FindBlocks(observer, "stone", radius: 24, max: 100);

        Assert.True(results.Count <= 64);
    }

    [Fact]
    public void FindEntity_FindsNearestEntityOfKind_ExcludingObserver()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(entityRadius: 32);

        var observerNpc = new NpcState(new EntityId(1), "Self", new Vec3(0f, 1f, 0f));
        var nearNpc = new NpcState(new EntityId(2), "Near", new Vec3(5f, 1f, 0f));
        var farNpc = new NpcState(new EntityId(3), "Far", new Vec3(12f, 1f, 0f));

        registry.Register(observerNpc);
        registry.Register(nearNpc);
        registry.Register(farNpc);

        var nearest = perception.FindEntity(observerNpc.Position, EntityKind.Npc, excludeId: observerNpc.Id);

        Assert.NotNull(nearest);
        Assert.Equal(nearNpc.Id, nearest.Value);
    }

    [Fact]
    public void FindEntity_ReturnsNull_WhenBeyondEntityRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(entityRadius: 32);

        var observer = new Vec3(0f, 1f, 0f);
        var farEntity = new PlayerEntity(new EntityId(2), "FarPlayer", new Vec3(40f, 1f, 0f));
        registry.Register(farEntity);

        var nearest = perception.FindEntity(observer, EntityKind.Player);
        Assert.Null(nearest);
    }

    [Fact]
    public void FindEntities_OrdersNearestFirst_ExcludesObserver_AndRespectsRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(entityRadius: 32);

        var observerNpc = new NpcState(new EntityId(1), "Self", new Vec3(0f, 1f, 0f));
        var item1 = new ItemEntity(new EntityId(2), new ItemStack("dirt", 1), new Vec3(10f, 1f, 0f));
        var item2 = new ItemEntity(new EntityId(3), new ItemStack("dirt", 1), new Vec3(3f, 1f, 0f));
        var item3 = new ItemEntity(new EntityId(4), new ItemStack("dirt", 1), new Vec3(20f, 1f, 0f));
        var itemFar = new ItemEntity(new EntityId(5), new ItemStack("dirt", 1), new Vec3(50f, 1f, 0f)); // outside radius

        registry.Register(observerNpc);
        registry.Register(item1);
        registry.Register(item2);
        registry.Register(item3);
        registry.Register(itemFar);

        var results = perception.FindEntities(observerNpc.Position, EntityKind.Item, excludeId: observerNpc.Id);

        Assert.Equal(3, results.Count);
        Assert.Equal(item2.Id, results[0]); // dist 3
        Assert.Equal(item1.Id, results[1]); // dist 10
        Assert.Equal(item3.Id, results[2]); // dist 20
    }

    [Fact]
    public void GetEntity_ReturnsSnapshot_WhenWithinRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(entityRadius: 32);

        var observer = new Vec3(0f, 1f, 0f);
        var targetNpc = new NpcState(new EntityId(2), "Bob", new Vec3(4f, 1f, 0f), health: 85f);
        registry.Register(targetNpc);

        var snapshot = perception.GetEntity(observer, targetNpc.Id);

        Assert.NotNull(snapshot);
        Assert.Equal(targetNpc.Id, snapshot.Id);
        Assert.Equal(EntityKind.Npc, snapshot.Kind);
        Assert.Equal("npc", snapshot.KindString);
        Assert.Equal("Bob", snapshot.Name);
        Assert.Equal(85f, snapshot.Health);
        Assert.Null(snapshot.ItemId);
        Assert.Null(snapshot.Count);
    }

    [Fact]
    public void GetEntity_ReturnsItemDetails_ForDroppedItems()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(entityRadius: 32);

        var observer = new Vec3(0f, 1f, 0f);
        var item = new ItemEntity(new EntityId(2), new ItemStack("oak_log", 12), new Vec3(4f, 1f, 0f));
        registry.Register(item);

        var snapshot = perception.GetEntity(observer, item.Id);

        Assert.NotNull(snapshot);
        Assert.Equal(item.Id, snapshot.Id);
        Assert.Equal(EntityKind.Item, snapshot.Kind);
        Assert.Equal("item", snapshot.KindString);
        Assert.Equal("oak_log", snapshot.ItemId);
        Assert.Equal(12, snapshot.Count);
    }

    [Fact]
    public void GetEntity_ReturnsNull_WhenBeyondRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(entityRadius: 32);

        var observer = new Vec3(0f, 1f, 0f);
        var target = new PlayerEntity(new EntityId(2), "Alice", new Vec3(45f, 1f, 0f));
        registry.Register(target);

        var snapshot = perception.GetEntity(observer, target.Id);
        Assert.Null(snapshot);
    }

    [Fact]
    public void IsWalkable_ReturnsTrueWithinRadius_FalseBeyondRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");

        // Solid floor at (5, 0, 0), air at (5, 1, 0) and (5, 2, 0)
        world.SetBlock(5, 0, 0, stone);
        // Solid floor at (30, 0, 0), air at (30, 1, 0) and (30, 2, 0)
        world.SetBlock(30, 0, 0, stone);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);

        Assert.True(perception.IsWalkable(observer, new Vec3i(5, 1, 0)));
        Assert.False(perception.IsWalkable(observer, new Vec3i(30, 1, 0))); // beyond radius
    }

    [Fact]
    public void GetSurface_FindsSurfaceWithinRadius_ReturnsNullBeyondRadius()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");

        world.SetBlock(5, 0, 0, stone);
        world.SetBlock(30, 0, 0, stone);

        var observer = new Vec3(0.5f, 1.0f, 0.5f);

        var surfaceNear = perception.GetSurface(observer, 5, 0);
        Assert.NotNull(surfaceNear);
        Assert.Equal(new Vec3i(5, 1, 0), surfaceNear.Value);

        var surfaceFar = perception.GetSurface(observer, 30, 0);
        Assert.Null(surfaceFar);
    }

    [Fact]
    public void TimeAndDay_ReflectsWorldTime()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv();

        perception.WorldTime.TimeOfDayHours = 14.5f;
        Assert.Equal(14.5f, perception.GetTime());
        Assert.True(perception.IsDay());

        perception.WorldTime.TimeOfDayHours = 22.0f;
        Assert.Equal(22.0f, perception.GetTime());
        Assert.False(perception.IsDay());
    }

    [Fact]
    public void For_ScopesQueriesToNpcObserverPosition()
    {
        var (world, blocks, bus, registry, perception) = CreateTestEnv(blockRadius: 24);
        ushort stone = blocks.GetId("stone");
        var blockPos = new Vec3i(10, 1, 0);
        world.SetBlock(blockPos, stone);

        var npc = new NpcState(new EntityId(1), "Wanderer", new Vec3(0f, 1f, 0f));
        registry.Register(npc);

        var npcPerception = perception.For(npc);

        // Initially at (0, 1, 0) -> dist to block (10, 1, 0) is ~10 <= 24 -> sees it
        Assert.Equal("stone", npcPerception.GetBlock(blockPos));

        // NPC moves away to (40, 1, 0) -> dist to block is 30 > 24 -> cannot see it
        npc.Position = new Vec3(40f, 1f, 0f);
        Assert.Null(npcPerception.GetBlock(blockPos));
    }
}
