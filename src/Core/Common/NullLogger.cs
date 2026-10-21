namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// No-op implementation of ILogger.
/// </summary>
public sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    public string Category => "Null";
    public LogLevel MinimumLevel { get; set; } = LogLevel.None;

    public bool IsEnabled(LogLevel level) => false;
    public void Log(LogLevel level, string message, Exception? exception = null) { }
    public void LogTrace(string message, Exception? exception = null) { }
    public void LogDebug(string message, Exception? exception = null) { }
    public void LogInfo(string message, Exception? exception = null) { }
    public void LogWarning(string message, Exception? exception = null) { }
    public void LogError(string message, Exception? exception = null) { }
}
