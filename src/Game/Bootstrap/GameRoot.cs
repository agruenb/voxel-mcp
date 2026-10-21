namespace VoxelKing.Game.Bootstrap;

using Godot;
using VoxelKing.Core.Common;

public partial class GameRoot : Node
{
    public IGameConfigProvider ConfigProvider { get; private set; } = null!;

    public override void _Ready()
    {
        string basePath = ProjectSettings.GlobalizePath("res://config/game_config.json");
        string userPath = ProjectSettings.GlobalizePath("user://config.json");

        var config = GameConfigLoader.LoadFromFiles(basePath, userPath);
        ConfigProvider = new ConfigProvider(config);

        GD.Print("started");
    }
}
