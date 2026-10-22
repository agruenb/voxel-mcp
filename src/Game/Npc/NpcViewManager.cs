namespace VoxelKing.Game.Npc;

using System;
using System.Collections.Generic;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;

/// <summary>
/// Manages visual NpcNode instances in the Godot scene tree in response to Core simulation events.
/// </summary>
public partial class NpcViewManager : Node3D
{
    private readonly Dictionary<EntityId, NpcNode> _views = new();
    private IDisposable? _spawnSub;
    private IDisposable? _despawnSub;
    private IDisposable? _spokeSub;
    private NpcManager? _npcManager;

    public IReadOnlyDictionary<EntityId, NpcNode> Views => _views;

    public void Initialize(IEventBus eventBus, NpcManager npcManager)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(npcManager);

        _npcManager = npcManager;

        _spawnSub = eventBus.Subscribe<EntitySpawned>(OnEntitySpawned);
        _despawnSub = eventBus.Subscribe<EntityDespawned>(OnEntityDespawned);
        _spokeSub = eventBus.Subscribe<EntitySpoke>(OnEntitySpoke);

        // Synchronize with any NPCs already spawned
        foreach (var npc in npcManager.ActiveNpcs)
        {
            CreateNpcView(npc);
        }
    }

    public NpcNode? GetView(EntityId id)
    {
        return _views.TryGetValue(id, out var node) ? node : null;
    }

    private void OnEntitySpawned(EntitySpawned e)
    {
        if (e.Entity is NpcState npc)
        {
            CreateNpcView(npc);
        }
    }

    private void OnEntityDespawned(EntityDespawned e)
    {
        if (e.Kind == EntityKind.Npc && _views.Remove(e.Id, out var node))
        {
            node.QueueFree();
        }
    }

    private void CreateNpcView(NpcState npc)
    {
        if (_views.ContainsKey(npc.Id))
        {
            return;
        }

        var node = new NpcNode { Name = $"Npc_{npc.Id}_{npc.Name}" };
        AddChild(node);
        node.BindState(npc);
        _views[npc.Id] = node;
    }

    private void OnEntitySpoke(EntitySpoke e)
    {
        if (_views.TryGetValue(e.SpeakerId, out var node))
        {
            node.ShowSpeechBubble(e.Text);
        }
    }

    public override void _ExitTree()
    {
        _spawnSub?.Dispose();
        _despawnSub?.Dispose();
        _spokeSub?.Dispose();
        _spawnSub = null;
        _despawnSub = null;
        _spokeSub = null;
    }
}
