namespace VoxelKing.Core.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Configurable factory for instantiating categorized Logger instances sharing sinks.
/// </summary>
public sealed class LoggerFactory : ILoggerFactory
{
    private readonly List<ILogSink> _sinks = new();
    private readonly object _lock = new();

    public LogLevel MinimumLevel { get; set; }

    public LoggerFactory(LogLevel minimumLevel = LogLevel.Info, IEnumerable<ILogSink>? sinks = null)
    {
        MinimumLevel = minimumLevel;
        if (sinks != null)
        {
            _sinks.AddRange(sinks);
        }
    }

    public void AddSink(ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (_lock)
        {
            _sinks.Add(sink);
        }
    }

    public ILogger CreateLogger(string category)
    {
        lock (_lock)
        {
            return new Logger(category, _sinks.ToArray(), MinimumLevel);
        }
    }

    public ILogger CreateLogger<T>() => CreateLogger(typeof(T).Name);
}
