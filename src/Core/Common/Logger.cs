namespace VoxelKing.Core.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Default implementation of ILogger dispatching log entries to one or more sinks.
/// </summary>
public sealed class Logger : ILogger
{
    private readonly IReadOnlyList<ILogSink> _sinks;

    public string Category { get; }
    public LogLevel MinimumLevel { get; set; }

    public Logger(string category, IEnumerable<ILogSink> sinks, LogLevel minimumLevel = LogLevel.Info)
    {
        Category = category ?? string.Empty;
        _sinks = new List<ILogSink>(sinks);
        MinimumLevel = minimumLevel;
    }

    public Logger(string category, ILogSink sink, LogLevel minimumLevel = LogLevel.Info)
        : this(category, new[] { sink ?? throw new ArgumentNullException(nameof(sink)) }, minimumLevel)
    {
    }

    public bool IsEnabled(LogLevel level)
    {
        if (MinimumLevel == LogLevel.None || level == LogLevel.None)
        {
            return false;
        }

        return level >= MinimumLevel;
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        for (int i = 0; i < _sinks.Count; i++)
        {
            _sinks[i].Emit(level, Category, message, exception);
        }
    }

    public void LogTrace(string message, Exception? exception = null) => Log(LogLevel.Trace, message, exception);
    public void LogDebug(string message, Exception? exception = null) => Log(LogLevel.Debug, message, exception);
    public void LogInfo(string message, Exception? exception = null) => Log(LogLevel.Info, message, exception);
    public void LogWarning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);
    public void LogError(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
}
