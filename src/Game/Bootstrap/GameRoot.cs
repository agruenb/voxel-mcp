namespace VoxelKing.Game.Bootstrap;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Game.Common;

public partial class GameRoot : Node
{
    private FileLogSink? _fileLogSink;

    public IGameConfigProvider ConfigProvider { get; private set; } = null!;
    public ILoggerFactory LoggerFactory { get; private set; } = null!;
    public ILogger Logger { get; private set; } = null!;
    public IEventBus EventBus { get; private set; } = null!;
    public IMainThreadDispatcher Dispatcher { get; private set; } = null!;

    public override void _Ready()
    {
        string basePath = ProjectSettings.GlobalizePath("res://config/game_config.json");
        string userPath = ProjectSettings.GlobalizePath("user://config.json");

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

        GD.Print("started");
        Logger.LogInfo("GameRoot initialized successfully");
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
