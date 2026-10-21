namespace VoxelKing.Core.Common;

/// <summary>
/// Factory for creating categorized ILogger instances.
/// </summary>
public interface ILoggerFactory
{
    /// <summary>
    /// Creates a logger configured for the specified category name.
    /// </summary>
    ILogger CreateLogger(string category);

    /// <summary>
    /// Creates a logger configured for the specified type's name as category.
    /// </summary>
    ILogger CreateLogger<T>();
}
