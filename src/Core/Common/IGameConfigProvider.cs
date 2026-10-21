namespace VoxelKing.Core.Common;

/// <summary>
/// Provides access to the current game configuration.
/// Injected into systems and services that require configuration values (no static globals).
/// </summary>
public interface IGameConfigProvider
{
    GameConfig Config { get; }
    event Action<GameConfig>? ConfigChanged;
}
