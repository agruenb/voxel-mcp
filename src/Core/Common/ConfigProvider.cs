namespace VoxelKing.Core.Common;

/// <summary>
/// In-memory implementation of <see cref="IGameConfigProvider"/>.
/// Can be injected into any system requiring configuration.
/// </summary>
public sealed class ConfigProvider : IGameConfigProvider
{
    public GameConfig Config { get; private set; }

    public event Action<GameConfig>? ConfigChanged;

    public ConfigProvider(GameConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void Update(GameConfig newConfig)
    {
        Config = newConfig ?? throw new ArgumentNullException(nameof(newConfig));
        ConfigChanged?.Invoke(Config);
    }
}
