namespace VoxelKing.Game.Bootstrap;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Perception;
using VoxelKing.Core.Persistence;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Common;
using VoxelKing.Game.Debug;
using VoxelKing.Game.Entities;
using VoxelKing.Game.Npc;
using VoxelKing.Game.Player;
using VoxelKing.Game.UI;
using VoxelKing.Game.World;

public partial class GameRoot : Node
{
    private FileLogSink? _fileLogSink;
    private float _autosaveTimer;

    public ISaveManager SaveManager { get; private set; } = null!;
    public string CurrentSaveDirectory { get; private set; } = string.Empty;

    public IGameConfigProvider ConfigProvider { get; private set; } = null!;
    public ILoggerFactory LoggerFactory { get; private set; } = null!;
    public ILogger Logger { get; private set; } = null!;
    public IEventBus EventBus { get; private set; } = null!;
    public IMainThreadDispatcher Dispatcher { get; private set; } = null!;
    public IBlockRegistry BlockRegistry { get; private set; } = null!;
    public IVoxelWorld VoxelWorld { get; private set; } = null!;
    public ITerrainGenerator TerrainGenerator { get; private set; } = null!;
    public IChunkMesher ChunkMesher { get; private set; } = null!;
    public ChunkStreamer ChunkStreamer { get; private set; } = null!;
    public ContainerManager ContainerManager { get; private set; } = new();
    public NamedPlaces NamedPlaces { get; private set; } = new();
    public EntityRegistry EntityRegistry { get; private set; } = null!;
    public IPathfindingService PathfindingService { get; private set; } = null!;
    public IPerceptionService PerceptionService { get; private set; } = null!;
    public NpcManager NpcManager { get; private set; } = null!;
    public NpcViewManager NpcViewManager { get; private set; } = null!;
    public ItemViewManager ItemViewManager { get; private set; } = null!;
    public DebugPathRenderer DebugPathRenderer { get; private set; } = null!;
    public DebugConsole DebugConsole { get; private set; } = null!;
    public DebugBrainConsole DebugBrainConsole { get; private set; } = null!;
    public NpcInspectorOverlay NpcInspectorOverlay { get; private set; } = null!;
    public PlayerEntity? PlayerEntity { get; private set; }
    public HotbarUI? HotbarUI { get; private set; }
    public ChestUI? ChestUI { get; private set; }
    public ChatLogUI? ChatLogUI { get; private set; }
    public DialogueUI? DialogueUI { get; private set; }
    public ILuaRuntime LuaRuntime { get; private set; } = null!;
    public ILlmClient LlmClient { get; private set; } = null!;
    public LlmRequestQueue LlmQueue { get; private set; } = null!;
    public IPromptBuilder PromptBuilder { get; private set; } = null!;
    public IBrainValidator BrainValidator { get; private set; } = null!;
    public IBrainCompiler BrainCompiler { get; private set; } = null!;
    public DayNightCycle DayNightCycle { get; private set; } = null!;

    public override void _Ready()
    {
        string basePath = ProjectSettings.GlobalizePath("res://config/game_config.json");
        string userPath = ProjectSettings.GlobalizePath("user://config.json");
        if (!File.Exists(userPath))
        {
            string localWorkspaceUserConfig = ProjectSettings.GlobalizePath("res://config/user_config.json");
            if (File.Exists(localWorkspaceUserConfig))
            {
                userPath = localWorkspaceUserConfig;
            }
        }
        string blocksPath = ProjectSettings.GlobalizePath("res://config/blocks.json");

        var config = GameConfigLoader.LoadFromFiles(basePath, userPath);
        ConfigProvider = new ConfigProvider(config);

        var minimumLogLevel = Enum.TryParse<LogLevel>(config.Debug.LogLevel, ignoreCase: true, out var parsedLevel)
            ? parsedLevel
            : LogLevel.Info;

        var loggerFactory = new LoggerFactory(minimumLogLevel);
        loggerFactory.AddSink(new GodotLogSink());

        try
        {
            string logFilePath = ProjectSettings.GlobalizePath("user://logs/game.log");
            _fileLogSink = new FileLogSink(logFilePath);
            loggerFactory.AddSink(_fileLogSink);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to initialize file log sink: {ex.Message}");
        }

        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger("GameRoot");
        EventBus = new EventBus();
        Dispatcher = new MainThreadDispatcher(loggerFactory.CreateLogger<MainThreadDispatcher>());

        if (File.Exists(blocksPath))
        {
            try
            {
                BlockRegistry = VoxelKing.Core.Voxel.BlockRegistry.LoadFromFile(blocksPath);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to load blocks from {blocksPath}, falling back to defaults: {ex.Message}");
                BlockRegistry = VoxelKing.Core.Voxel.BlockRegistry.CreateDefault();
            }
        }
        else
        {
            BlockRegistry = VoxelKing.Core.Voxel.BlockRegistry.CreateDefault();
        }

        VoxelWorld = new VoxelWorld(BlockRegistry, EventBus);
        PathfindingService = new PathfindingService(VoxelWorld);
        TerrainGenerator = new TerrainGenerator(config.World, BlockRegistry);
        ChunkMesher = new ChunkMesher(BlockRegistry);

        EntityRegistry = new EntityRegistry(config.World.PerceptionRadiusBlocks);
        PerceptionService = new PerceptionService(VoxelWorld, EntityRegistry, config.World);

        NpcManager = new NpcManager(
            EntityRegistry,
            config.Npc,
            VoxelWorld,
            EventBus,
            LoggerFactory.CreateLogger<NpcManager>(),
            PathfindingService,
            PerceptionService,
            config.BrainLimits
        )
        {
            FreezeDistance = config.World.LoadedChunkRadius * config.World.ChunkSize,
            ContainerManager = ContainerManager,
            BlockRegistry = BlockRegistry
        };

        LuaRuntime = new MoonSharpRuntime(config.BrainLimits);

        var httpClient = new System.Net.Http.HttpClient();
        var geminiClient = new GeminiClient(httpClient, config.Llm, loggerFactory.CreateLogger<GeminiClient>());
        LlmClient = geminiClient;

        string llmLogDir = ProjectSettings.GlobalizePath(config.Debug.LlmLogDirectory);
        ILlmRequestLogger requestLogger = config.Debug.LogLlmRequests
            ? new FileLlmRequestLogger(llmLogDir, loggerFactory.CreateLogger<FileLlmRequestLogger>())
            : NullLlmRequestLogger.Instance;

        LlmQueue = new LlmRequestQueue(
            geminiClient,
            config.Llm,
            requestLogger,
            loggerFactory.CreateLogger<LlmRequestQueue>());

        PromptBuilder = new PromptBuilder(LuaApiRegistry.Default);
        BrainValidator = new BrainValidator(config.BrainLimits);
        BrainCompiler = new BrainCompiler(
            LlmQueue,
            PromptBuilder,
            BrainValidator,
            Dispatcher,
            NpcManager.SpeechSystem,
            EntityRegistry,
            NpcManager,
            config.Llm,
            loggerFactory.CreateLogger<BrainCompiler>());

        SaveManager = new SaveManager(loggerFactory.CreateLogger<SaveManager>());
        string savesBase = ProjectSettings.GlobalizePath(config.Save.SavesDirectory);
        CurrentSaveDirectory = Path.Combine(savesBase, config.Save.WorldName);

        bool hasSave = config.Save.Enabled && SaveManager.HasSave(CurrentSaveDirectory);
        if (hasSave)
        {
            try
            {
                var worldData = SaveManager.LoadWorldMetadata(CurrentSaveDirectory);
                if (worldData != null)
                {
                    SaveManager.RestoreNamedPlaces(NamedPlaces, worldData.NamedPlaces);
                    SaveManager.RestoreContainers(ContainerManager, worldData.Containers);
                    if (PerceptionService != null)
                    {
                        PerceptionService.WorldTime.TotalElapsedSeconds = worldData.WorldTime;
                    }
                    Logger.LogInfo($"Restored {worldData.Containers.Count} containers and {worldData.NamedPlaces.Count} named places from save.");
                }

                var modifiedChunks = SaveManager.LoadModifiedChunks(CurrentSaveDirectory, BlockRegistry.IsIndexed);
                foreach (var chunk in modifiedChunks)
                {
                    VoxelWorld.AddOrUpdateChunk(chunk);
                }
                Logger.LogInfo($"Loaded {modifiedChunks.Count} modified chunks from save.");

                var npcData = SaveManager.LoadNpcs(CurrentSaveDirectory);
                if (npcData != null && npcData.Npcs.Count > 0)
                {
                    foreach (var npcDto in npcData.Npcs)
                    {
                        var npc = SaveManager.CreateNpcState(npcDto, config.BrainLimits);
                        NpcManager.RestoreNpc(npc, npcDto.BrainSource);
                    }
                    Logger.LogInfo($"Restored {npcData.Npcs.Count} NPCs from save.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading save from '{CurrentSaveDirectory}': {ex.Message}");
            }
        }

        ChunkStreamer = GetNodeOrNull<ChunkStreamer>("ChunkStreamer") ?? new ChunkStreamer();
        if (ChunkStreamer.GetParent() == null)
        {
            AddChild(ChunkStreamer);
        }

        var player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
        {
            player.VoxelWorld = VoxelWorld;
            player.BlockRegistry = BlockRegistry;
            player.ContainerManager = ContainerManager;
            player.NamedPlaces = NamedPlaces;

            HotbarUI = new HotbarUI { Name = "HotbarUI" };
            AddChild(HotbarUI);
            HotbarUI.BindPlayer(player, EventBus);
            player.HotbarUI = HotbarUI;

            ChestUI = new ChestUI { Name = "ChestUI" };
            AddChild(ChestUI);
            ChestUI.Setup(player, NamedPlaces, HotbarUI);
            player.ChestUI = ChestUI;

            player.ApplyConfig(config.Player);

            bool restoredPlayer = false;
            if (hasSave)
            {
                try
                {
                    var playerData = SaveManager.LoadPlayer(CurrentSaveDirectory);
                    if (playerData != null)
                    {
                        player.GlobalPosition = new Vector3(playerData.Position.X, playerData.Position.Y, playerData.Position.Z);
                        player.SetLookRotation(playerData.Yaw, playerData.Pitch);
                        player.IsFlying = playerData.IsFlying;
                        player.SelectedHotbarSlot = playerData.SelectedHotbarSlot;
                        InventorySlotDto.PopulateInventory(player.Inventory, playerData.Inventory);
                        HotbarUI?.UpdateHotbarVisuals();
                        restoredPlayer = true;
                        Logger.LogInfo($"Restored player at {playerData.Position} from save.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error loading player save: {ex.Message}");
                }
            }

            if (!restoredPlayer)
            {
                player.SpawnOnSurface(TerrainGenerator, 0, 0, VoxelWorld);
                Logger.LogInfo($"Player initialized and spawned at {player.GlobalPosition}");
            }

            ChunkStreamer.FocusNode = player;

            PlayerEntity = new PlayerEntity(
                EntityRegistry.GenerateNextId(),
                "Player",
                new Vec3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z),
                player.Inventory
            );
            EntityRegistry.Register(PlayerEntity);
            player.PlayerEntity = PlayerEntity;
        }
        else
        {
            var camera = GetNodeOrNull<Camera3D>("Camera3D");
            if (camera != null)
            {
                ChunkStreamer.FocusNode = camera;
            }
        }

        ChunkStreamer.Initialize(
            VoxelWorld,
            TerrainGenerator,
            ChunkMesher,
            Dispatcher,
            EventBus,
            LoggerFactory.CreateLogger<ChunkStreamer>(),
            config.World
        );

        NpcViewManager = new NpcViewManager { Name = "NpcViewManager" };
        AddChild(NpcViewManager);
        NpcViewManager.Initialize(EventBus, NpcManager);

        ItemViewManager = new ItemViewManager { Name = "ItemViewManager" };
        AddChild(ItemViewManager);
        ItemViewManager.Initialize(EventBus, NpcManager, BlockRegistry);

        ChatLogUI = new ChatLogUI { Name = "ChatLogUI" };
        AddChild(ChatLogUI);
        ChatLogUI.Initialize(EventBus);

        DebugPathRenderer = new DebugPathRenderer { Name = "DebugPathRenderer" };
        AddChild(DebugPathRenderer);
        DebugPathRenderer.Initialize(NpcManager);

        var sunLight = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
        var worldEnv = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        DayNightCycle = new DayNightCycle { Name = "DayNightCycle" };
        AddChild(DayNightCycle);
        if (PerceptionService?.WorldTime != null)
        {
            DayNightCycle.Initialize(PerceptionService.WorldTime, config.World, sunLight, worldEnv);
        }

        DebugConsole = new DebugConsole
        {
            Name = "DebugConsole",
            NpcManager = NpcManager,
            VoxelWorld = VoxelWorld,
            Player = player,
            PathfindingService = PathfindingService,
            PathRenderer = DebugPathRenderer,
            WorldTime = PerceptionService?.WorldTime,
            DayNightCycle = DayNightCycle
        };
        AddChild(DebugConsole);

        BrainCompiler.ResultApplied += (npc, result) =>
        {
            if (result.Status == CompileStatus.Error)
            {
                DebugConsole?.Log($"[LLM Error] NPC '{npc.Name}' (ID {npc.Id}): {result.Error?.Kind} - {result.Error?.Message}");
            }
            else if (result.Status == CompileStatus.ValidationFailed)
            {
                string firstErr = result.Validation?.Errors != null && result.Validation.Errors.Count > 0
                    ? result.Validation.Errors[0].Message
                    : "Validation failed";
                DebugConsole?.Log($"[Brain Validation Failed] NPC '{npc.Name}' (ID {npc.Id}): {firstErr}");
            }
        };

        DialogueUI = new DialogueUI { Name = "DialogueUI" };
        AddChild(DialogueUI);
        if (player != null)
        {
            DialogueUI.Setup(player, NpcManager, BrainCompiler, PlayerEntity);
            player.DialogueUI = DialogueUI;
            player.NpcManager = NpcManager;

            if (NpcManager.ActiveNpcs.Count == 0)
            {
                SpawnInitialNpcs(player);
            }
        }

        DebugBrainConsole = new DebugBrainConsole
        {
            Name = "DebugBrainConsole",
            NpcManager = NpcManager,
            Player = player,
            LuaRuntime = LuaRuntime
        };
        AddChild(DebugBrainConsole);

        NpcInspectorOverlay = new NpcInspectorOverlay
        {
            Name = "NpcInspectorOverlay",
            NpcManager = NpcManager,
            Player = player,
            BrainCompiler = BrainCompiler,
            LlmQueue = LlmQueue,
            PathRenderer = DebugPathRenderer,
            WorldTime = PerceptionService?.WorldTime
        };
        AddChild(NpcInspectorOverlay);
        if (player != null)
        {
            player.NpcInspectorOverlay = NpcInspectorOverlay;
        }

        GD.Print("started");
        Logger.LogInfo("GameRoot initialized successfully");
        Logger.LogInfo($"BlockRegistry loaded with {BlockRegistry.Count} block types");
        Logger.LogInfo($"TerrainGenerator initialized with seed {config.World.Seed}");
        Logger.LogInfo("ChunkMesher initialized");
        Logger.LogInfo("NpcManager and EntityRegistry initialized");
        Logger.LogInfo("NpcViewManager, DebugPathRenderer, DebugConsole, and DebugBrainConsole initialized");
        Logger.LogInfo("LlmRequestQueue initialized successfully");
        Logger.LogInfo("BrainCompiler initialized successfully");
    }

    private void SpawnInitialNpcs(PlayerController player)
    {
        var playerPos = new Vec3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z);
        IReadOnlyList<NpcProfile> profiles;

        string npcsPath = ProjectSettings.GlobalizePath("res://config/npcs.json");
        if (File.Exists(npcsPath))
        {
            try
            {
                profiles = NpcProfileLoader.LoadFromFile(npcsPath);
                Logger.LogInfo($"Loaded {profiles.Count} NPC profiles from '{npcsPath}'.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to load npcs.json: {ex.Message}. Falling back to default profiles.");
                profiles = NpcProfileLoader.LoadDefaultProfiles();
            }
        }
        else
        {
            string? found = NpcProfileLoader.FindConfigFile();
            if (found != null && File.Exists(found))
            {
                profiles = NpcProfileLoader.LoadFromFile(found);
                Logger.LogInfo($"Loaded {profiles.Count} NPC profiles from '{found}'.");
            }
            else
            {
                Logger.LogInfo("npcs.json not found. Using default NPC profiles.");
                profiles = NpcProfileLoader.LoadDefaultProfiles();
            }
        }

        foreach (var profile in profiles)
        {
            float targetX = playerPos.X + profile.HomeOffset.X;
            float targetZ = playerPos.Z + profile.HomeOffset.Z;
            float spawnY = playerPos.Y + profile.HomeOffset.Y;

            if (TerrainGenerator != null)
            {
                float surfaceY = TerrainGenerator.GetHeight((int)MathF.Floor(targetX), (int)MathF.Floor(targetZ));
                spawnY = surfaceY + 1.0f;
            }

            var spawnPos = new Vec3(targetX, spawnY, targetZ);
            var npc = NpcManager.SpawnNpc(profile, spawnPos);
            npc.Home = spawnPos;

            // Give basic starter materials
            npc.Inventory?.AddItem("stone", 20, out _);
            npc.Inventory?.AddItem("oak_log", 20, out _);
            npc.Inventory?.AddItem("dirt", 20, out _);

            Logger.LogInfo($"Spawned NPC '{npc.Name}' ({npc.Personality}) at {spawnPos} with color {npc.AppearanceColor} running '{profile.InitialBrain ?? "idle"}'");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (PlayerEntity != null)
        {
            var player = GetNodeOrNull<PlayerController>("Player");
            if (player != null)
            {
                PlayerEntity.Position = new Vec3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z);
                EntityRegistry.UpdateSpatial(PlayerEntity);
            }
        }

        NpcManager.PhysicsTick(dt);
        NpcManager.BrainTick(dt);
    }

    public override void _Process(double delta)
    {
        Dispatcher.Drain(TimeSpan.FromMilliseconds(4));
        DayNightCycle?.Advance((float)delta);

        if (ConfigProvider?.Config?.Save != null && ConfigProvider.Config.Save.Enabled && ConfigProvider.Config.Save.AutosaveIntervalSeconds > 0)
        {
            _autosaveTimer += (float)delta;
            if (_autosaveTimer >= ConfigProvider.Config.Save.AutosaveIntervalSeconds)
            {
                _autosaveTimer = 0f;
                SaveGame();
            }
        }
    }

    public void SaveGame(string? worldName = null)
    {
        if (SaveManager == null) return;

        string targetDir = string.IsNullOrWhiteSpace(worldName)
            ? CurrentSaveDirectory
            : Path.Combine(ProjectSettings.GlobalizePath(ConfigProvider.Config.Save.SavesDirectory), worldName);

        var player = GetNodeOrNull<PlayerController>("Player");
        PlayerSaveData? playerData = null;
        if (player != null)
        {
            var playerPos = player.IsInsideTree()
                ? new Vec3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z)
                : (PlayerEntity?.Position ?? new Vec3(player.Position.X, player.Position.Y, player.Position.Z));

            playerData = new PlayerSaveData
            {
                Version = 1,
                Position = playerPos,
                Yaw = player.Yaw,
                Pitch = player.Pitch,
                IsFlying = player.IsFlying,
                SelectedHotbarSlot = player.SelectedHotbarSlot,
                Inventory = InventorySlotDto.FromInventory(player.Inventory)
            };
        }

        var worldData = new WorldSaveData
        {
            Version = 1,
            WorldName = worldName ?? ConfigProvider.Config.Save.WorldName,
            Seed = ConfigProvider.Config.World.Seed,
            WorldTime = PerceptionService?.WorldTime.TotalElapsedSeconds ?? 0.0,
            SavedAtUtc = DateTime.UtcNow,
            NamedPlaces = SaveManager.CreateNamedPlacesDto(NamedPlaces),
            Containers = SaveManager.CreateContainerDtos(ContainerManager)
        };

        SaveManager.SaveWorld(
            targetDir,
            worldData,
            VoxelWorld.ModifiedChunks,
            NpcManager.ActiveNpcs,
            playerData);

        Logger.LogInfo($"Saved game to '{targetDir}'");
    }

    public bool LoadGame(string? worldName = null)
    {
        if (SaveManager == null) return false;

        string targetDir = string.IsNullOrWhiteSpace(worldName)
            ? CurrentSaveDirectory
            : Path.Combine(ProjectSettings.GlobalizePath(ConfigProvider.Config.Save.SavesDirectory), worldName);

        if (!SaveManager.HasSave(targetDir))
        {
            Logger.LogWarning($"Cannot load world '{targetDir}': save not found.");
            return false;
        }

        try
        {
            var worldData = SaveManager.LoadWorldMetadata(targetDir);
            if (worldData != null)
            {
                SaveManager.RestoreNamedPlaces(NamedPlaces, worldData.NamedPlaces);
                SaveManager.RestoreContainers(ContainerManager, worldData.Containers);
                if (PerceptionService != null)
                {
                    PerceptionService.WorldTime.TotalElapsedSeconds = worldData.WorldTime;
                    DayNightCycle?.UpdateVisuals();
                }
            }

            var modifiedChunks = SaveManager.LoadModifiedChunks(targetDir, BlockRegistry.IsIndexed);
            foreach (var chunk in modifiedChunks)
            {
                VoxelWorld.AddOrUpdateChunk(chunk);
            }

            NpcManager.DespawnAllNpcs();
            var npcData = SaveManager.LoadNpcs(targetDir);
            if (npcData != null)
            {
                foreach (var npcDto in npcData.Npcs)
                {
                    var npc = SaveManager.CreateNpcState(npcDto, ConfigProvider.Config.BrainLimits);
                    NpcManager.RestoreNpc(npc, npcDto.BrainSource);
                }
            }

            var player = GetNodeOrNull<PlayerController>("Player");
            if (player != null)
            {
                var playerData = SaveManager.LoadPlayer(targetDir);
                if (playerData != null)
                {
                    player.GlobalPosition = new Vector3(playerData.Position.X, playerData.Position.Y, playerData.Position.Z);
                    player.SetLookRotation(playerData.Yaw, playerData.Pitch);
                    player.IsFlying = playerData.IsFlying;
                    player.SelectedHotbarSlot = playerData.SelectedHotbarSlot;
                    InventorySlotDto.PopulateInventory(player.Inventory, playerData.Inventory);
                    HotbarUI?.UpdateHotbarVisuals();
                    if (PlayerEntity != null)
                    {
                        PlayerEntity.Position = playerData.Position;
                    }
                }
            }

            Logger.LogInfo($"Loaded game from '{targetDir}' successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load game from '{targetDir}': {ex.Message}");
            return false;
        }
    }

    public override void _ExitTree()
    {
        if (ConfigProvider?.Config?.Save != null && ConfigProvider.Config.Save.Enabled)
        {
            try
            {
                SaveGame();
            }
            catch (Exception ex)
            {
                GD.PushWarning($"Failed to save game on exit: {ex.Message}");
            }
        }

        LlmQueue?.Dispose();
        NpcManager?.Dispose();
        _fileLogSink?.Dispose();
        _fileLogSink = null;
    }
}
