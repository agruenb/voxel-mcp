namespace VoxelKing.Core.Persistence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Voxel;

/// <summary>
/// Service interface for saving and loading world state, modified terrain, chests, NPCs, and player data.
/// </summary>
public interface ISaveManager
{
    SaveMigrationManager MigrationManager { get; }

    bool HasSave(string saveDirectory);

    void SaveWorld(
        string saveDirectory,
        WorldSaveData worldData,
        IEnumerable<ChunkData> chunks,
        IEnumerable<NpcState> npcs,
        PlayerSaveData? playerData);

    WorldSaveData? LoadWorldMetadata(string saveDirectory);

    IReadOnlyList<ChunkData> LoadModifiedChunks(string saveDirectory, Func<ushort, bool>? isIndexed = null);

    NpcSaveData? LoadNpcs(string saveDirectory);

    PlayerSaveData? LoadPlayer(string saveDirectory);

    NpcSaveDto CreateNpcSaveDto(NpcState npc);
    NpcState CreateNpcState(NpcSaveDto dto, BrainLimitsConfig? limitsConfig = null);
    List<ContainerSaveDto> CreateContainerDtos(ContainerManager containerManager);
    void RestoreContainers(ContainerManager containerManager, IEnumerable<ContainerSaveDto>? containers);
    Dictionary<string, Vec3i> CreateNamedPlacesDto(NamedPlaces namedPlaces);
    void RestoreNamedPlaces(NamedPlaces namedPlaces, IDictionary<string, Vec3i>? places);
}

/// <summary>
/// Authoritative implementation of world and simulation persistence (ARCHITECTURE §5.7).
/// </summary>
public sealed class SaveManager : ISaveManager
{
    public const string WorldFileName = "world.json";
    public const string NpcsFileName = "npcs.json";

    NpcSaveDto ISaveManager.CreateNpcSaveDto(NpcState npc) => CreateNpcSaveDto(npc);
    NpcState ISaveManager.CreateNpcState(NpcSaveDto dto, BrainLimitsConfig? limitsConfig) => CreateNpcState(dto, limitsConfig);
    List<ContainerSaveDto> ISaveManager.CreateContainerDtos(ContainerManager containerManager) => CreateContainerDtos(containerManager);
    void ISaveManager.RestoreContainers(ContainerManager containerManager, IEnumerable<ContainerSaveDto>? containers) => RestoreContainers(containerManager, containers);
    Dictionary<string, Vec3i> ISaveManager.CreateNamedPlacesDto(NamedPlaces namedPlaces) => CreateNamedPlacesDto(namedPlaces);
    void ISaveManager.RestoreNamedPlaces(NamedPlaces namedPlaces, IDictionary<string, Vec3i>? places) => RestoreNamedPlaces(namedPlaces, places);
    public const string PlayerFileName = "player.json";
    public const string RegionsDirName = "regions";

    private readonly ILogger? _logger;

    public SaveMigrationManager MigrationManager { get; } = new();

    public SaveManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    public bool HasSave(string saveDirectory)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory)) return false;
        string worldPath = Path.Combine(saveDirectory, WorldFileName);
        return File.Exists(worldPath);
    }

    public void SaveWorld(
        string saveDirectory,
        WorldSaveData worldData,
        IEnumerable<ChunkData> chunks,
        IEnumerable<NpcState> npcs,
        PlayerSaveData? playerData)
    {
        ArgumentNullException.ThrowIfNull(saveDirectory);
        ArgumentNullException.ThrowIfNull(worldData);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(npcs);

        Directory.CreateDirectory(saveDirectory);

        // 1. Save world metadata (world.json)
        string worldJson = worldData.ToJson();
        WriteFileAtomic(Path.Combine(saveDirectory, WorldFileName), worldJson);

        // 2. Save modified chunks (regions/chunk_{x}_{y}_{z}.bin)
        string regionsDir = Path.Combine(saveDirectory, RegionsDirName);
        Directory.CreateDirectory(regionsDir);

        int savedChunkCount = 0;
        foreach (var chunk in chunks)
        {
            if (!chunk.IsModified)
            {
                continue;
            }

            string chunkFileName = $"chunk_{chunk.ChunkCoord.X}_{chunk.ChunkCoord.Y}_{chunk.ChunkCoord.Z}.bin";
            string chunkPath = Path.Combine(regionsDir, chunkFileName);
            byte[] bytes = RleChunkSerializer.Serialize(chunk);
            WriteFileBytesAtomic(chunkPath, bytes);
            savedChunkCount++;
        }

        // 3. Save NPCs (npcs.json)
        var npcSaveData = new NpcSaveData
        {
            Version = 1,
            Npcs = npcs.Select(CreateNpcSaveDto).ToList()
        };
        string npcsJson = npcSaveData.ToJson();
        WriteFileAtomic(Path.Combine(saveDirectory, NpcsFileName), npcsJson);

        // 4. Save player (player.json) if provided
        if (playerData != null)
        {
            string playerJson = playerData.ToJson();
            WriteFileAtomic(Path.Combine(saveDirectory, PlayerFileName), playerJson);
        }

        _logger?.LogInfo($"World '{worldData.WorldName}' saved successfully to '{saveDirectory}' ({savedChunkCount} modified chunks, {npcSaveData.Npcs.Count} NPCs).");
    }

    public WorldSaveData? LoadWorldMetadata(string saveDirectory)
    {
        string worldPath = Path.Combine(saveDirectory, WorldFileName);
        if (!File.Exists(worldPath)) return null;

        string json = File.ReadAllText(worldPath);
        json = MigrationManager.MigrateWorldJson(json);
        return WorldSaveData.FromJson(json);
    }

    public IReadOnlyList<ChunkData> LoadModifiedChunks(string saveDirectory, Func<ushort, bool>? isIndexed = null)
    {
        string regionsDir = Path.Combine(saveDirectory, RegionsDirName);
        if (!Directory.Exists(regionsDir))
        {
            return Array.Empty<ChunkData>();
        }

        var files = Directory.GetFiles(regionsDir, "chunk_*.bin");
        var list = new List<ChunkData>(files.Length);

        foreach (var file in files)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                var chunk = RleChunkSerializer.Deserialize(bytes, isIndexed);
                list.Add(chunk);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to load chunk file '{file}': {ex.Message}");
            }
        }

        return list;
    }

    public NpcSaveData? LoadNpcs(string saveDirectory)
    {
        string npcsPath = Path.Combine(saveDirectory, NpcsFileName);
        if (!File.Exists(npcsPath)) return null;

        string json = File.ReadAllText(npcsPath);
        json = MigrationManager.MigrateNpcsJson(json);
        return NpcSaveData.FromJson(json);
    }

    public PlayerSaveData? LoadPlayer(string saveDirectory)
    {
        string playerPath = Path.Combine(saveDirectory, PlayerFileName);
        if (!File.Exists(playerPath)) return null;

        string json = File.ReadAllText(playerPath);
        json = MigrationManager.MigratePlayerJson(json);
        return PlayerSaveData.FromJson(json);
    }

    public static NpcSaveDto CreateNpcSaveDto(NpcState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        var dto = new NpcSaveDto
        {
            Id = npc.Id.Value,
            Name = npc.Name,
            Profile = npc.Profile,
            Personality = npc.Personality,
            Skills = npc.Skills.ToList(),
            AppearanceColor = npc.AppearanceColor,
            Position = npc.Position,
            Velocity = npc.Velocity,
            Facing = npc.Facing,
            Health = npc.Health,
            MaxHealth = npc.MaxHealth,
            Home = npc.Home,
            HasExplicitHome = npc.HasExplicitHome,
            CurrentTaskSummary = npc.CurrentTaskSummary,
            TaskStartTime = npc.TaskStartTime,
            InstructorId = npc.InstructorId?.Value,
            Inventory = InventorySlotDto.FromInventory(npc.Inventory),
            BrainSource = npc.BrainHost?.CurrentScript
        };

        if (npc.Memory is NpcMemory mem)
        {
            dto.Memory = JsonSerializer.Deserialize<NpcMemoryDto>(mem.ToJson(), PersistenceJsonOptions.Default) ?? new NpcMemoryDto();
        }

        if (npc.ConversationHistory.Count > 0)
        {
            dto.ConversationHistory = npc.ConversationHistory
                .Select(t => new ConversationTurnDto(t.Speaker, t.Message))
                .ToList();
        }

        return dto;
    }

    public static NpcState CreateNpcState(NpcSaveDto dto, BrainLimitsConfig? limitsConfig = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var id = new EntityId(dto.Id);
        string memoryJson = JsonSerializer.Serialize(dto.Memory, PersistenceJsonOptions.Default);
        var memory = NpcMemory.FromJson(memoryJson, limitsConfig);

        var inventory = new Inventory(36);
        InventorySlotDto.PopulateInventory(inventory, dto.Inventory);

        var npc = new NpcState(
            id,
            dto.Name,
            dto.Position,
            profile: dto.Profile,
            home: dto.Home,
            inventory: inventory,
            health: dto.Health,
            facing: dto.Facing,
            memory: memory,
            personality: dto.Personality,
            skills: dto.Skills,
            appearanceColor: dto.AppearanceColor
        )
        {
            Velocity = dto.Velocity,
            MaxHealth = dto.MaxHealth,
            HasExplicitHome = dto.HasExplicitHome,
            CurrentTaskSummary = dto.CurrentTaskSummary,
            TaskStartTime = dto.TaskStartTime,
            InstructorId = dto.InstructorId.HasValue ? new EntityId(dto.InstructorId.Value) : null
        };

        if (dto.ConversationHistory != null)
        {
            foreach (var turn in dto.ConversationHistory)
            {
                npc.AddConversationTurn(turn.Speaker, turn.Message);
            }
        }

        return npc;
    }

    public static List<ContainerSaveDto> CreateContainerDtos(ContainerManager containerManager)
    {
        ArgumentNullException.ThrowIfNull(containerManager);

        var list = new List<ContainerSaveDto>();
        foreach (var (pos, inv) in containerManager.AllContainers)
        {
            list.Add(new ContainerSaveDto
            {
                Position = pos,
                SlotCount = inv.SlotCount,
                Slots = InventorySlotDto.FromInventory(inv)
            });
        }
        return list;
    }

    public static void RestoreContainers(ContainerManager containerManager, IEnumerable<ContainerSaveDto>? containers)
    {
        ArgumentNullException.ThrowIfNull(containerManager);
        containerManager.Clear();
        if (containers == null) return;

        foreach (var dto in containers)
        {
            var inv = containerManager.GetOrCreateContainer(dto.Position, dto.SlotCount);
            InventorySlotDto.PopulateInventory(inv, dto.Slots);
        }
    }

    public static Dictionary<string, Vec3i> CreateNamedPlacesDto(NamedPlaces namedPlaces)
    {
        ArgumentNullException.ThrowIfNull(namedPlaces);
        return new Dictionary<string, Vec3i>(namedPlaces.AllPlaces, StringComparer.OrdinalIgnoreCase);
    }

    public static void RestoreNamedPlaces(NamedPlaces namedPlaces, IDictionary<string, Vec3i>? places)
    {
        ArgumentNullException.ThrowIfNull(namedPlaces);
        namedPlaces.Clear();
        if (places == null) return;

        foreach (var (k, v) in places)
        {
            namedPlaces.SetPlace(k, v);
        }
    }

    private static void WriteFileAtomic(string filePath, string content)
    {
        string tmpPath = filePath + ".tmp";
        File.WriteAllText(tmpPath, content);
        File.Move(tmpPath, filePath, overwrite: true);
    }

    private static void WriteFileBytesAtomic(string filePath, byte[] bytes)
    {
        string tmpPath = filePath + ".tmp";
        File.WriteAllBytes(tmpPath, bytes);
        File.Move(tmpPath, filePath, overwrite: true);
    }
}
