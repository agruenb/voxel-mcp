namespace VoxelKing.Core.Tests.Entities;

using VoxelKing.Core.Entities;
using Xunit;

public class EntityIdTests
{
    [Fact]
    public void EntityId_Equality_And_Comparison()
    {
        var id1 = new EntityId(5);
        var id2 = new EntityId(5);
        var id3 = new EntityId(10);
        var none = EntityId.None;

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.True(id1 < id3);
        Assert.True(id3 > id1);
        Assert.Equal(0, id1.CompareTo(id2));
        Assert.True(id1.CompareTo(id3) < 0);

        Assert.True(id1.IsValid);
        Assert.False(none.IsValid);
    }

    [Fact]
    public void EntityId_Conversions_And_Formatting()
    {
        var id = new EntityId(42);
        int intVal = id;
        EntityId fromInt = (EntityId)intVal;

        Assert.Equal(42, intVal);
        Assert.Equal(id, fromInt);
        Assert.Equal("42", id.ToString());
    }

    [Fact]
    public void EntityKind_Conversions_And_Parsing()
    {
        Assert.Equal("player", EntityKind.Player.ToApiString());
        Assert.Equal("npc", EntityKind.Npc.ToApiString());
        Assert.Equal("item", EntityKind.Item.ToApiString());
        Assert.Equal("animal", EntityKind.Animal.ToApiString());

        Assert.True(EntityKindExtensions.TryParse("player", out var k1));
        Assert.Equal(EntityKind.Player, k1);

        Assert.True(EntityKindExtensions.TryParse("NPC", out var k2));
        Assert.Equal(EntityKind.Npc, k2);

        Assert.True(EntityKindExtensions.TryParse("item", out var k3));
        Assert.Equal(EntityKind.Item, k3);

        Assert.True(EntityKindExtensions.TryParse("animal", out var k4));
        Assert.Equal(EntityKind.Animal, k4);

        Assert.False(EntityKindExtensions.TryParse("unknown", out _));
        Assert.False(EntityKindExtensions.TryParse(null, out _));
    }
}
