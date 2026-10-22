namespace VoxelKing.Core.Npc.Arbiter;

/// <summary>
/// Stub task layer implementation used until F-303 integrates the Lua BrainHost.
/// </summary>
public sealed class StubTaskLayer : ITaskLayer
{
    public bool HasActiveTask => false;

    public void Tick(float dt)
    {
    }

    public void Interrupt()
    {
    }
}
