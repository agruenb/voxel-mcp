namespace VoxelKing.Game.Bootstrap;

using System;
using System.IO;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Perception;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Common;
using VoxelKing.Game.Debug;
using VoxelKing.Game.Npc;
using VoxelKing.Game.Player;
using VoxelKing.Game.UI;
using VoxelKing.Game.World;

public partial class GameRoot : Node
{
    private FileLogSink? _fileLogSink;

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
    public DebugPathRenderer DebugPathRenderer { get; private set; } = null!;
    public DebugConsole DebugConsole { get; private set; } = null!;
    public PlayerEntity? PlayerEntity { get; private set; }
    public HotbarUI? HotbarUI { get; private set; }
    public ChestUI? ChestUI { get; private set; }

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
            HotbarUI.BindPlayer(player);
            player.HotbarUI = HotbarUI;

            ChestUI = new ChestUI { Name = "ChestUI" };
            AddChild(ChestUI);
            ChestUI.Setup(player, NamedPlaces, HotbarUI);
            player.ChestUI = ChestUI;

            player.ApplyConfig(config.Player);
            player.SpawnOnSurface(TerrainGenerator, 0, 0, VoxelWorld);
            ChunkStreamer.FocusNode = player;

            PlayerEntity = new PlayerEntity(
                EntityRegistry.GenerateNextId(),
                "Player",
                new Vec3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z),
                player.Inventory
            );
            EntityRegistry.Register(PlayerEntity);

            Logger.LogInfo($"Player initialized and spawned at {player.GlobalPosition}");
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

        DebugPathRenderer = new DebugPathRenderer { Name = "DebugPathRenderer" };
        AddChild(DebugPathRenderer);
        DebugPathRenderer.Initialize(NpcManager);

        DebugConsole = new DebugConsole
        {
            Name = "DebugConsole",
            NpcManager = NpcManager,
            VoxelWorld = VoxelWorld,
            Player = player,
            PathfindingService = PathfindingService,
            PathRenderer = DebugPathRenderer
        };
        AddChild(DebugConsole);

        GD.Print("started");
        Logger.LogInfo("GameRoot initialized successfully");
        Logger.LogInfo($"BlockRegistry loaded with {BlockRegistry.Count} block types");
        Logger.LogInfo($"TerrainGenerator initialized with seed {config.World.Seed}");
        Logger.LogInfo("ChunkMesher initialized");
        Logger.LogInfo("NpcManager and EntityRegistry initialized");
        Logger.LogInfo("NpcViewManager, DebugPathRenderer, and DebugConsole initialized");
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
    }

    public override void _ExitTree()
    {
        _fileLogSink?.Dispose();
        _fileLogSink = null;
    }
}
