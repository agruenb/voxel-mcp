namespace VoxelKing.Game.Entities;

using System;
using System.Collections.Generic;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.World;

/// <summary>
/// Manages visual ItemNode instances in the Godot scene tree in response to Core item lifecycle events.
/// </summary>
public partial class ItemViewManager : Node3D
{
    private readonly Dictionary<EntityId, ItemNode> _views = new();
    private readonly Dictionary<string, ArrayMesh> _meshCache = new();
    private ChunkMaterials? _materials;
    private StandardMaterial3D? _opaqueMaterial;
    private StandardMaterial3D? _transparentMaterial;
    private IDisposable? _spawnSub;
    private IDisposable? _despawnSub;
    private NpcManager? _npcManager;
    private IBlockRegistry? _blockRegistry;

    public IReadOnlyDictionary<EntityId, ItemNode> Views => _views;

    public void Initialize(IEventBus eventBus, NpcManager npcManager, IBlockRegistry? blockRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(npcManager);

        _npcManager = npcManager;
        _blockRegistry = blockRegistry ?? npcManager.BlockRegistry;

        _materials = new ChunkMaterials();
        _opaqueMaterial = new StandardMaterial3D
        {
            AlbedoTexture = _materials.AtlasTexture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 1.0f
        };
        _transparentMaterial = new StandardMaterial3D
        {
            AlbedoTexture = _materials.AtlasTexture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 1.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _spawnSub = eventBus.Subscribe<EntitySpawned>(OnEntitySpawned);
        _despawnSub = eventBus.Subscribe<EntityDespawned>(OnEntityDespawned);

        // Synchronize with any item entities already spawned in simulation
        foreach (var item in npcManager.ActiveItems)
        {
            CreateItemView(item);
        }
    }

    public ItemNode? GetView(EntityId id)
    {
        return _views.TryGetValue(id, out var node) ? node : null;
    }

    private void OnEntitySpawned(EntitySpawned e)
    {
        if (e.Entity is ItemEntity item)
        {
            CreateItemView(item);
        }
    }

    private void OnEntityDespawned(EntityDespawned e)
    {
        if (e.Kind == EntityKind.Item && _views.Remove(e.Id, out var node))
        {
            node.QueueFree();
        }
    }

    private void CreateItemView(ItemEntity item)
    {
        if (_views.ContainsKey(item.Id))
        {
            return;
        }

        var mesh = GetOrCreateMesh(item.Stack.ItemId, out bool isTransparent);
        var material = isTransparent ? (_transparentMaterial ?? _opaqueMaterial!) : (_opaqueMaterial!);

        var node = new ItemNode { Name = $"Item_{item.Id}_{item.Stack.ItemId}" };
        AddChild(node);
        node.BindEntity(item, mesh, material);
        _views[item.Id] = node;
    }

    private ArrayMesh GetOrCreateMesh(string itemId, out bool isTransparent)
    {
        isTransparent = false;
        if (_meshCache.TryGetValue(itemId, out var cachedMesh))
        {
            if (_blockRegistry != null && _blockRegistry.TryGetId(itemId, out ushort id))
            {
                var def = _blockRegistry.GetDefinition(id);
                isTransparent = def.IsTransparent;
            }
            return cachedMesh;
        }

        BlockDefinition? blockDef = null;
        if (_blockRegistry != null && _blockRegistry.TryGetId(itemId, out ushort blockId))
        {
            blockDef = _blockRegistry.GetDefinition(blockId);
            isTransparent = blockDef.IsTransparent;
        }

        var mesh = ItemMeshBuilder.BuildBlockItemMesh(blockDef);
        _meshCache[itemId] = mesh;
        return mesh;
    }

    public override void _ExitTree()
    {
        _spawnSub?.Dispose();
        _spawnSub = null;

        _despawnSub?.Dispose();
        _despawnSub = null;

        foreach (var node in _views.Values)
        {
            node.QueueFree();
        }
        _views.Clear();
        _meshCache.Clear();
    }
}
