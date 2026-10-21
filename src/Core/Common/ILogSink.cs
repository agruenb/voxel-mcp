namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// Destination sink for formatted log events.
/// </summary>
public interface ILogSink
{
    /// <summary>
    /// Emits a log event to the sink.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="category">The category or component generating the entry.</param>
    /// <param name="message">The log message text.</param>
    /// <param name="exception">Optional exception associated with the log entry.</param>
    void Emit(LogLevel level, string category, string message, Exception? exception = null);
}
