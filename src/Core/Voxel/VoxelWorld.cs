namespace VoxelKing.Core.Voxel;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VoxelKing.Core.Common;

/// <summary>
/// Central voxel world simulation owning chunk storage, block mutations, border invalidation, and change events.
/// </summary>
public sealed class VoxelWorld : IVoxelWorld
{
    private readonly ConcurrentDictionary<Vec3i, ChunkData> _chunks = new();
    private readonly IEventBus? _eventBus;

    public IBlockRegistry BlockRegistry { get; }
    public int ChunkCount => _chunks.Count;
    public IEnumerable<ChunkData> Chunks => _chunks.Values;

    public VoxelWorld(IBlockRegistry blockRegistry, IEventBus? eventBus = null)
    {
        BlockRegistry = blockRegistry ?? throw new ArgumentNullException(nameof(blockRegistry));
        _eventBus = eventBus;
    }

    public ushort GetBlock(Vec3i worldPos)
    {
        var (chunkCoord, localCoord) = VoxelCoordinates.WorldToChunkAndLocal(worldPos);
        if (_chunks.TryGetValue(chunkCoord, out var chunk))
        {
            return chunk.GetBlock(localCoord);
        }
        return BlockRegistry.AirId;
    }

    public ushort GetBlock(int worldX, int worldY, int worldZ) =>
        GetBlock(new Vec3i(worldX, worldY, worldZ));

    public bool SetBlock(Vec3i worldPos, ushort blockId)
    {
        var (chunkCoord, localCoord) = VoxelCoordinates.WorldToChunkAndLocal(worldPos);

        if (!_chunks.TryGetValue(chunkCoord, out var chunk))
        {
            if (blockId == BlockRegistry.AirId)
            {
                return false;
            }
            chunk = GetOrCreateChunk(chunkCoord);
        }

        ushort oldBlock = chunk.GetBlock(localCoord);
        if (oldBlock == blockId)
        {
            return false;
        }

        chunk.SetBlock(localCoord, blockId);
        chunk.IsDirty = true;

        // Border dirty logic: mark adjacent loaded neighbor chunks dirty when border voxels change
        foreach (var offset in VoxelCoordinates.GetBorderNeighborChunkOffsets(localCoord))
        {
            var neighborCoord = chunkCoord + offset;
            if (_chunks.TryGetValue(neighborCoord, out var neighborChunk))
            {
                neighborChunk.IsDirty = true;
            }
        }

        _eventBus?.Publish(new BlockChanged(worldPos, oldBlock, blockId));
        return true;
    }

    public bool SetBlock(int worldX, int worldY, int worldZ, ushort blockId) =>
        SetBlock(new Vec3i(worldX, worldY, worldZ), blockId);

    public bool HasChunk(Vec3i chunkCoord) => _chunks.ContainsKey(chunkCoord);

    public ChunkData? GetChunk(Vec3i chunkCoord) =>
        _chunks.TryGetValue(chunkCoord, out var chunk) ? chunk : null;

    public ChunkData GetOrCreateChunk(Vec3i chunkCoord) =>
        _chunks.GetOrAdd(chunkCoord, coord => new ChunkData(coord, BlockRegistry.IsIndexed));

    public void AddOrUpdateChunk(ChunkData chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.BlockIndex.IsIndexedPredicate == null)
        {
            chunk.BlockIndex.SetIndexPredicate(BlockRegistry.IsIndexed, chunk.Blocks);
        }
        _chunks[chunk.ChunkCoord] = chunk;
    }

    public bool RemoveChunk(Vec3i chunkCoord) =>
        _chunks.TryRemove(chunkCoord, out _);

    public Vec3i? FindNearest(string blockId, Vec3i origin, int radius)
    {
        ArgumentNullException.ThrowIfNull(blockId);
        return BlockRegistry.TryGetId(blockId, out ushort id)
            ? FindNearest(id, origin, radius)
            : null;
    }

    public Vec3i? FindNearest(ushort blockId, Vec3i origin, int radius)
    {
        if (radius < 0)
        {
            return null;
        }

        long radiusSq = (long)radius * radius;
        int minX = origin.X - radius; int maxX = origin.X + radius;
        int minY = origin.Y - radius; int maxY = origin.Y + radius;
        int minZ = origin.Z - radius; int maxZ = origin.Z + radius;
        Vec3i minChunk = VoxelCoordinates.WorldToChunk(new Vec3i(minX, minY, minZ));
        Vec3i maxChunk = VoxelCoordinates.WorldToChunk(new Vec3i(maxX, maxY, maxZ));

        List<(ChunkData Chunk, int BoxDistSq)> candidateChunks = new();

        for (int cz = minChunk.Z; cz <= maxChunk.Z; cz++)
        {
            for (int cy = minChunk.Y; cy <= maxChunk.Y; cy++)
            {
                for (int cx = minChunk.X; cx <= maxChunk.X; cx++)
                {
                    var coord = new Vec3i(cx, cy, cz);
                    if (!_chunks.TryGetValue(coord, out var chunk))
                    {
                        continue;
                    }

                    if (chunk.GetBlockCount(blockId) == 0)
                    {
                        continue;
                    }

                    Vec3i chunkMin = coord * ChunkData.Size;
                    Vec3i chunkMax = chunkMin + new Vec3i(ChunkData.Size - 1, ChunkData.Size - 1, ChunkData.Size - 1);
                    int boxDistSq = DistanceSquaredToBox(origin, chunkMin, chunkMax);
                    if (boxDistSq <= radiusSq)
                    {
                        candidateChunks.Add((chunk, boxDistSq));
                    }
                }
            }
        }

        if (candidateChunks.Count == 0)
        {
            return null;
        }

        candidateChunks.Sort((a, b) => a.BoxDistSq.CompareTo(b.BoxDistSq));

        Vec3i? bestPos = null;
        long bestDistSq = long.MaxValue;
        bool isIndexed = BlockRegistry.IsIndexed(blockId);

        foreach (var (chunk, boxDistSq) in candidateChunks)
        {
            if (boxDistSq > bestDistSq)
            {
                break;
            }

            if (isIndexed)
            {
                foreach (var localPos in chunk.BlockIndex.GetPositions(blockId))
                {
                    Vec3i worldPos = VoxelCoordinates.LocalToWorld(chunk.ChunkCoord, localPos);
                    long dx = (long)worldPos.X - origin.X;
                    long dy = (long)worldPos.Y - origin.Y;
                    long dz = (long)worldPos.Z - origin.Z;
                    long distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    if (distSq <= radiusSq)
                    {
                        if (distSq < bestDistSq || (distSq == bestDistSq && CompareCoords(worldPos, bestPos!.Value) < 0))
                        {
                            bestDistSq = distSq;
                            bestPos = worldPos;
                        }
                    }
                }
            }
            else
            {
                var blocks = chunk.Blocks;
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i] == blockId)
                    {
                        Vec3i localPos = ChunkData.GetLocalCoords(i);
                        Vec3i worldPos = VoxelCoordinates.LocalToWorld(chunk.ChunkCoord, localPos);
                        long dx = (long)worldPos.X - origin.X;
                        long dy = (long)worldPos.Y - origin.Y;
                        long dz = (long)worldPos.Z - origin.Z;
                        long distSq = (dx * dx) + (dy * dy) + (dz * dz);

                        if (distSq <= radiusSq)
                        {
                            if (distSq < bestDistSq || (distSq == bestDistSq && CompareCoords(worldPos, bestPos!.Value) < 0))
                            {
                                bestDistSq = distSq;
                                bestPos = worldPos;
                            }
                        }
                    }
                }
            }
        }

        return bestPos;
    }

    public IReadOnlyList<Vec3i> FindAll(string blockId, Vec3i origin, int radius, int max = 64)
    {
        ArgumentNullException.ThrowIfNull(blockId);
        return BlockRegistry.TryGetId(blockId, out ushort id)
            ? FindAll(id, origin, radius, max)
            : Array.Empty<Vec3i>();
    }

    public IReadOnlyList<Vec3i> FindAll(ushort blockId, Vec3i origin, int radius, int max = 64)
    {
        if (radius < 0 || max <= 0)
        {
            return Array.Empty<Vec3i>();
        }

        long radiusSq = (long)radius * radius;
        int minX = origin.X - radius; int maxX = origin.X + radius;
        int minY = origin.Y - radius; int maxY = origin.Y + radius;
        int minZ = origin.Z - radius; int maxZ = origin.Z + radius;
        Vec3i minChunk = VoxelCoordinates.WorldToChunk(new Vec3i(minX, minY, minZ));
        Vec3i maxChunk = VoxelCoordinates.WorldToChunk(new Vec3i(maxX, maxY, maxZ));

        List<(ChunkData Chunk, int BoxDistSq)> candidateChunks = new();

        for (int cz = minChunk.Z; cz <= maxChunk.Z; cz++)
        {
            for (int cy = minChunk.Y; cy <= maxChunk.Y; cy++)
            {
                for (int cx = minChunk.X; cx <= maxChunk.X; cx++)
                {
                    var coord = new Vec3i(cx, cy, cz);
                    if (!_chunks.TryGetValue(coord, out var chunk))
                    {
                        continue;
                    }

                    if (chunk.GetBlockCount(blockId) == 0)
                    {
                        continue;
                    }

                    Vec3i chunkMin = coord * ChunkData.Size;
                    Vec3i chunkMax = chunkMin + new Vec3i(ChunkData.Size - 1, ChunkData.Size - 1, ChunkData.Size - 1);
                    int boxDistSq = DistanceSquaredToBox(origin, chunkMin, chunkMax);
                    if (boxDistSq <= radiusSq)
                    {
                        candidateChunks.Add((chunk, boxDistSq));
                    }
                }
            }
        }

        if (candidateChunks.Count == 0)
        {
            return Array.Empty<Vec3i>();
        }

        candidateChunks.Sort((a, b) => a.BoxDistSq.CompareTo(b.BoxDistSq));

        var heap = new (Vec3i Pos, long DistSq)[max];
        int heapCount = 0;
        bool isIndexed = BlockRegistry.IsIndexed(blockId);

        foreach (var (chunk, boxDistSq) in candidateChunks)
        {
            // Prune: if we already have max elements and this chunk's closest voxel is strictly worse than
            // our furthest collected candidate, then no voxel in this chunk (or subsequent chunks) can qualify.
            if (heapCount == max && boxDistSq > heap[0].DistSq)
            {
                break;
            }

            if (isIndexed)
            {
                foreach (var localPos in chunk.BlockIndex.GetPositions(blockId))
                {
                    Vec3i worldPos = VoxelCoordinates.LocalToWorld(chunk.ChunkCoord, localPos);
                    long dx = (long)worldPos.X - origin.X;
                    long dy = (long)worldPos.Y - origin.Y;
                    long dz = (long)worldPos.Z - origin.Z;
                    long distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    if (distSq <= radiusSq)
                    {
                        PushCandidate(heap, ref heapCount, max, (worldPos, distSq));
                    }
                }
            }
            else
            {
                var blocks = chunk.Blocks;
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i] == blockId)
                    {
                        Vec3i localPos = ChunkData.GetLocalCoords(i);
                        Vec3i worldPos = VoxelCoordinates.LocalToWorld(chunk.ChunkCoord, localPos);
                        long dx = (long)worldPos.X - origin.X;
                        long dy = (long)worldPos.Y - origin.Y;
                        long dz = (long)worldPos.Z - origin.Z;
                        long distSq = (dx * dx) + (dy * dy) + (dz * dz);

                        if (distSq <= radiusSq)
                        {
                            PushCandidate(heap, ref heapCount, max, (worldPos, distSq));
                        }
                    }
                }
            }
        }

        if (heapCount == 0)
        {
            return Array.Empty<Vec3i>();
        }

        // Sort candidates nearest-first
        Array.Sort(heap, 0, heapCount, CandidateComparer.Instance);

        var list = new Vec3i[heapCount];
        for (int i = 0; i < heapCount; i++)
        {
            list[i] = heap[i].Pos;
        }

        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushCandidate(
        (Vec3i Pos, long DistSq)[] heap,
        ref int count,
        int max,
        (Vec3i Pos, long DistSq) item)
    {
        if (count < max)
        {
            heap[count] = item;
            SiftUp(heap, count);
            count++;
        }
        else if (IsWorse(heap[0], item))
        {
            // Root is strictly worse than item: replace root and sift down
            heap[0] = item;
            SiftDown(heap, count, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWorse((Vec3i Pos, long DistSq) a, (Vec3i Pos, long DistSq) b)
    {
        if (a.DistSq != b.DistSq)
        {
            return a.DistSq > b.DistSq;
        }
        return CompareCoords(a.Pos, b.Pos) > 0;
    }

    private static void SiftUp((Vec3i Pos, long DistSq)[] heap, int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (IsWorse(heap[index], heap[parent]))
            {
                var tmp = heap[index];
                heap[index] = heap[parent];
                heap[parent] = tmp;
                index = parent;
            }
            else
            {
                break;
            }
        }
    }

    private static void SiftDown((Vec3i Pos, long DistSq)[] heap, int count, int index)
    {
        while (true)
        {
            int left = (index << 1) + 1;
            int right = left + 1;
            int largest = index;

            if (left < count && IsWorse(heap[left], heap[largest]))
            {
                largest = left;
            }
            if (right < count && IsWorse(heap[right], heap[largest]))
            {
                largest = right;
            }

            if (largest != index)
            {
                var tmp = heap[index];
                heap[index] = heap[largest];
                heap[largest] = tmp;
                index = largest;
            }
            else
            {
                break;
            }
        }
    }

    private sealed class CandidateComparer : IComparer<(Vec3i Pos, long DistSq)>
    {
        public static readonly CandidateComparer Instance = new();

        public int Compare((Vec3i Pos, long DistSq) x, (Vec3i Pos, long DistSq) y)
        {
            int c = x.DistSq.CompareTo(y.DistSq);
            if (c != 0) return c;
            return CompareCoords(x.Pos, y.Pos);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DistanceSquaredToBox(Vec3i point, Vec3i boxMin, Vec3i boxMax)
    {
        int dx = 0;
        if (point.X < boxMin.X) dx = boxMin.X - point.X;
        else if (point.X > boxMax.X) dx = point.X - boxMax.X;

        int dy = 0;
        if (point.Y < boxMin.Y) dy = boxMin.Y - point.Y;
        else if (point.Y > boxMax.Y) dy = point.Y - boxMax.Y;

        int dz = 0;
        if (point.Z < boxMin.Z) dz = boxMin.Z - point.Z;
        else if (point.Z > boxMax.Z) dz = point.Z - boxMax.Z;

        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareCoords(Vec3i a, Vec3i b)
    {
        int c = a.X.CompareTo(b.X);
        if (c != 0) return c;
        c = a.Y.CompareTo(b.Y);
        if (c != 0) return c;
        return a.Z.CompareTo(b.Z);
    }
}
