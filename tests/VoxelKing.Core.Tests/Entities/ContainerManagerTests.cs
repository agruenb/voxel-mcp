namespace VoxelKing.Core.Tests.Entities;

using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using Xunit;

public class ContainerManagerTests
{
    [Fact]
    public void GetOrCreateContainer_ReturnsSameInstanceForPosition()
    {
        var mgr = new ContainerManager();
        var pos = new Vec3i(10, 5, -3);

        var c1 = mgr.GetOrCreateContainer(pos);
        var c2 = mgr.GetOrCreateContainer(pos);

        Assert.Same(c1, c2);
        Assert.True(mgr.HasContainer(pos));
        Assert.Equal(1, mgr.Count);
    }

    [Fact]
    public void RemoveContainer_CleansUpAndReturnsRemovedInventory()
    {
        var mgr = new ContainerManager();
        var pos = new Vec3i(1, 2, 3);
        var container = mgr.GetOrCreateContainer(pos);
        container.AddItem("chest", 1, out _);

        bool removed = mgr.RemoveContainer(pos, out var old);
        Assert.True(removed);
        Assert.Same(container, old);
        Assert.False(mgr.HasContainer(pos));
        Assert.Equal(0, mgr.Count);
    }

    [Fact]
    public void NamedPlaces_StoresAndRetrievesChestLocations()
    {
        var places = new NamedPlaces();
        var chestPos = new Vec3i(20, 65, 10);

        places.SetPlace("player_chest", chestPos);

        Assert.True(places.HasPlace("player_chest"));
        Assert.True(places.HasPlace("PLAYER_CHEST")); // case-insensitive
        Assert.Equal(chestPos, places.GetPlace("player_chest"));
        Assert.Null(places.GetPlace("unknown_place"));
    }
}
