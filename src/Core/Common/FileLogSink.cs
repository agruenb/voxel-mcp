namespace VoxelKing.Core.Common;

using System;
using System.IO;
using System.Text;

/// <summary>
/// Writes log entries to a local file with thread-safe access.
/// </summary>
public sealed class FileLogSink : ILogSink, IDisposable
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly StreamWriter _writer;
    private bool _disposed;

    public string FilePath => _filePath;

    public FileLogSink(string filePath, bool append = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public void Emit(LogLevel level, string category, string message, Exception? exception = null)
    {
        if (_disposed)
        {
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelString = level.ToString().ToUpperInvariant();
        var logLine = $"[{timestamp}] [{levelString,-5}] [{category}] {message}";

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine(logLine);
            if (exception != null)
            {
                _writer.WriteLine(exception.ToString());
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
