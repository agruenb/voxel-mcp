namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// Logger abstraction supporting categories, minimum levels, and structured emission.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// The category name associated with this logger instance.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Gets or sets the minimum log level required for messages to be processed.
    /// </summary>
    LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// Checks whether logging is enabled for the specified level.
    /// </summary>
    bool IsEnabled(LogLevel level);

    /// <summary>
    /// Logs a message at the specified log level.
    /// </summary>
    void Log(LogLevel level, string message, Exception? exception = null);

    /// <summary>
    /// Logs a trace-level message.
    /// </summary>
    void LogTrace(string message, Exception? exception = null);

    /// <summary>
    /// Logs a debug-level message.
    /// </summary>
    void LogDebug(string message, Exception? exception = null);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void LogInfo(string message, Exception? exception = null);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void LogWarning(string message, Exception? exception = null);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    void LogError(string message, Exception? exception = null);
}
