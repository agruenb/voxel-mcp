namespace VoxelKing.Game.World;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Common;

/// <summary>
/// Manages streaming, asynchronous generation, worker-threaded meshing, and lifecycle of chunk nodes around a focus point.
/// </summary>
public partial class ChunkStreamer : Node3D
{
    private readonly Dictionary<Vec3i, ChunkNode> _activeNodes = new();
    private readonly HashSet<Vec3i> _generatingCoords = new();
    private readonly HashSet<Vec3i> _meshingCoords = new();
    private readonly HashSet<Vec3i> _dirtyMeshCoords = new();
    private readonly Queue<Vec3i> _urgentReMeshQueue = new();

    private IVoxelWorld _voxelWorld = null!;
    private ITerrainGenerator _terrainGenerator = null!;
    private IChunkMesher _chunkMesher = null!;
    private IMainThreadDispatcher _dispatcher = null!;
    private IEventBus? _eventBus;
    private ILogger? _logger;
    private ChunkMaterials _materials = null!;
    private ChunkStreamPlanner _planner = null!;
    private IDisposable? _blockChangedSubscription;

    public int MaxConcurrentGenerations { get; set; } = 4;
    public int MaxConcurrentMeshings { get; set; } = 4;
    public Node3D? FocusNode { get; set; }
    public Vector3 FocusPosition { get; set; } = new(0, 64, 0);

    public int ActiveChunkCount => _activeNodes.Count;
    public int PendingGenerationCount => _generatingCoords.Count;
    public int PendingMeshingCount => _meshingCoords.Count;

    public ChunkStreamer()
    {
    }

    public void Initialize(
        IVoxelWorld voxelWorld,
        ITerrainGenerator terrainGenerator,
        IChunkMesher chunkMesher,
        IMainThreadDispatcher dispatcher,
        IEventBus? eventBus,
        ILogger? logger,
        WorldConfig worldConfig)
    {
        _voxelWorld = voxelWorld ?? throw new ArgumentNullException(nameof(voxelWorld));
        _terrainGenerator = terrainGenerator ?? throw new ArgumentNullException(nameof(terrainGenerator));
        _chunkMesher = chunkMesher ?? throw new ArgumentNullException(nameof(chunkMesher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _eventBus = eventBus;
        _logger = logger;

        _materials = new ChunkMaterials();
        _planner = new ChunkStreamPlanner(worldConfig, hysteresis: 1);

        _blockChangedSubscription = _eventBus?.Subscribe<BlockChanged>(OnBlockChanged);

        _logger?.LogInfo($"ChunkStreamer initialized with radius {_planner.Config.HorizontalRadius}, hysteresis {_planner.Config.Hysteresis}");
    }

    public override void _Process(double delta)
    {
        if (_voxelWorld == null || _terrainGenerator == null || _chunkMesher == null)
        {
            return;
        }

        Vector3 currentFocus = FocusNode?.GlobalPosition ?? FocusPosition;
        Vec3i centerChunk = VoxelCoordinates.WorldToChunk(currentFocus.ToCoreVec3i());

        ProcessUnloads(centerChunk);
        ProcessUrgentReMeshes(centerChunk);
        ProcessGeneration(centerChunk);
        ProcessMeshing(centerChunk);
    }

    private void ProcessUnloads(Vec3i centerChunk)
    {
        if (_activeNodes.Count == 0) return;

        var toUnload = _planner.GetChunksToUnload(centerChunk, _activeNodes.Keys);
        for (int i = 0; i < toUnload.Count; i++)
        {
            var coord = toUnload[i];
            if (_activeNodes.Remove(coord, out var node))
            {
                node.QueueFree();
            }
            _dirtyMeshCoords.Remove(coord);
        }
    }

    private void ProcessUrgentReMeshes(Vec3i centerChunk)
    {
        while (_urgentReMeshQueue.Count > 0 && _meshingCoords.Count < (MaxConcurrentMeshings * 2))
        {
            var coord = _urgentReMeshQueue.Dequeue();
            if (_meshingCoords.Contains(coord))
            {
                continue;
            }

            if (!_voxelWorld.HasChunk(coord))
            {
                continue;
            }

            StartMeshingTask(coord);
        }
    }

    private void ProcessGeneration(Vec3i centerChunk)
    {
        if (_generatingCoords.Count >= MaxConcurrentGenerations)
        {
            return;
        }

        var neededForGen = _planner.GetChunksInGenerationRadius(centerChunk);
        for (int i = 0; i < neededForGen.Count; i++)
        {
            var coord = neededForGen[i];
            if (_voxelWorld.HasChunk(coord) || _generatingCoords.Contains(coord))
            {
                continue;
            }

            _generatingCoords.Add(coord);
            Task.Run(() =>
            {
                var chunkData = _terrainGenerator.GenerateChunk(coord);
                _dispatcher.Enqueue(() => OnChunkGenerated(chunkData));
            });

            if (_generatingCoords.Count >= MaxConcurrentGenerations)
            {
                break;
            }
        }
    }

    private void OnChunkGenerated(ChunkData chunkData)
    {
        _generatingCoords.Remove(chunkData.ChunkCoord);
        _voxelWorld.AddOrUpdateChunk(chunkData);
        _dirtyMeshCoords.Add(chunkData.ChunkCoord);

        // Mark loaded neighbors dirty so boundary faces facing this new chunk are re-evaluated
        foreach (var offset in Vec3i.Directions6)
        {
            var neighborCoord = chunkData.ChunkCoord + offset;
            if (_activeNodes.ContainsKey(neighborCoord))
            {
                _dirtyMeshCoords.Add(neighborCoord);
            }
        }
    }

    private void ProcessMeshing(Vec3i centerChunk)
    {
        if (_meshingCoords.Count >= MaxConcurrentMeshings)
        {
            return;
        }

        var neededForMesh = _planner.GetChunksInLoadRadius(centerChunk);
        for (int i = 0; i < neededForMesh.Count; i++)
        {
            var coord = neededForMesh[i];

            bool needsMesh = !_activeNodes.ContainsKey(coord) || _dirtyMeshCoords.Contains(coord);
            if (!needsMesh || _meshingCoords.Contains(coord))
            {
                continue;
            }

            if (!_voxelWorld.HasChunk(coord))
            {
                continue; // Wait for generation
            }

            if (!AreNeighborsGenerated(coord))
            {
                continue; // Wait for neighbors to generate to avoid chunk border holes
            }

            StartMeshingTask(coord);

            if (_meshingCoords.Count >= MaxConcurrentMeshings)
            {
                break;
            }
        }
    }

    private void StartMeshingTask(Vec3i coord)
    {
        var chunk = _voxelWorld.GetChunk(coord);
        if (chunk == null) return;

        _meshingCoords.Add(coord);
        _dirtyMeshCoords.Remove(coord);

        // Snapshot is captured on main thread for worker thread safety
        var snapshot = ChunkMeshSnapshot.Create(chunk, _voxelWorld);

        _chunkMesher.MeshAsync(snapshot).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                _dispatcher.Enqueue(() => OnMeshReady(t.Result));
            }
            else
            {
                _dispatcher.Enqueue(() =>
                {
                    _meshingCoords.Remove(coord);
                    _logger?.LogError($"Error meshing chunk {coord}: {t.Exception?.GetBaseException().Message}");
                });
            }
        });
    }

    private void OnMeshReady(ChunkMeshData meshData)
    {
        _meshingCoords.Remove(meshData.ChunkCoord);

        // If chunk was marked dirty again while meshing (e.g. from a block edit), trigger re-mesh
        if (_dirtyMeshCoords.Contains(meshData.ChunkCoord))
        {
            StartMeshingTask(meshData.ChunkCoord);
        }

        Vector3 currentFocus = FocusNode?.GlobalPosition ?? FocusPosition;
        Vec3i centerChunk = VoxelCoordinates.WorldToChunk(currentFocus.ToCoreVec3i());

        if (_planner.IsBeyondUnloadRadius(meshData.ChunkCoord, centerChunk))
        {
            return; // Out of range while meshing
        }

        if (_activeNodes.TryGetValue(meshData.ChunkCoord, out var existingNode))
        {
            if (meshData.Version >= existingNode.MeshVersion)
            {
                existingNode.ApplyMesh(meshData, _materials.Opaque, _materials.Transparent);
            }
        }
        else
        {
            var newNode = new ChunkNode(meshData.ChunkCoord);
            newNode.ApplyMesh(meshData, _materials.Opaque, _materials.Transparent);
            AddChild(newNode);
            _activeNodes[meshData.ChunkCoord] = newNode;

            if (_activeNodes.Count % 25 == 1)
            {
                _logger?.LogInfo($"Streaming progress: {_activeNodes.Count} active chunk nodes rendered, {_voxelWorld.ChunkCount} generated in world");
            }
        }
    }

    private bool AreNeighborsGenerated(Vec3i coord)
    {
        foreach (var dir in Vec3i.Directions6)
        {
            var nCoord = coord + dir;
            if (nCoord.Y < _planner.Config.MinChunkY || nCoord.Y > _planner.Config.MaxChunkY)
            {
                continue;
            }

            if (!_voxelWorld.HasChunk(nCoord))
            {
                return false;
            }
        }

        return true;
    }

    private void OnBlockChanged(BlockChanged e)
    {
        var (chunkCoord, localCoord) = VoxelCoordinates.WorldToChunkAndLocal(e.Position);
        _dirtyMeshCoords.Add(chunkCoord);
        _urgentReMeshQueue.Enqueue(chunkCoord);

        foreach (var offset in VoxelCoordinates.GetBorderNeighborChunkOffsets(localCoord))
        {
            var neighborCoord = chunkCoord + offset;
            _dirtyMeshCoords.Add(neighborCoord);
            _urgentReMeshQueue.Enqueue(neighborCoord);
        }
    }

    public override void _ExitTree()
    {
        _blockChangedSubscription?.Dispose();
        _blockChangedSubscription = null;

        _logger?.LogInfo($"ChunkStreamer exit. Active chunk nodes: {_activeNodes.Count}, Total generated chunks in world: {_voxelWorld?.ChunkCount ?? 0}");

        foreach (var node in _activeNodes.Values)
        {
            node.QueueFree();
        }
        _activeNodes.Clear();
    }
}
