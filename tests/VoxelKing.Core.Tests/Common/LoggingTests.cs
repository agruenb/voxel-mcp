namespace VoxelKing.Core.Tests.Common;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using Xunit;

public class LoggingTests
{
    private sealed class TestLogSink : ILogSink
    {
        public record Entry(LogLevel Level, string Category, string Message, Exception? Exception);
        public List<Entry> Entries { get; } = new();

        public void Emit(LogLevel level, string category, string message, Exception? exception = null)
        {
            Entries.Add(new Entry(level, category, message, exception));
        }
    }

    [Fact]
    public void Logger_FiltersMessagesBelowMinimumLevel()
    {
        var sink = new TestLogSink();
        var logger = new Logger("TestCategory", sink, LogLevel.Info);

        logger.LogTrace("trace message");
        logger.LogDebug("debug message");
        logger.LogInfo("info message");
        logger.LogWarning("warning message");
        logger.LogError("error message");

        Assert.Equal(3, sink.Entries.Count);
        Assert.Equal(LogLevel.Info, sink.Entries[0].Level);
        Assert.Equal("info message", sink.Entries[0].Message);
        Assert.Equal("TestCategory", sink.Entries[0].Category);

        Assert.Equal(LogLevel.Warning, sink.Entries[1].Level);
        Assert.Equal("warning message", sink.Entries[1].Message);

        Assert.Equal(LogLevel.Error, sink.Entries[2].Level);
        Assert.Equal("error message", sink.Entries[2].Message);
    }

    [Fact]
    public void Logger_WithLogLevelNone_LogsNothing()
    {
        var sink = new TestLogSink();
        var logger = new Logger("Silent", sink, LogLevel.None);

        Assert.False(logger.IsEnabled(LogLevel.Error));
        logger.LogError("won't be logged");

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void LoggerFactory_CreatesCategorizedLoggers_WithSharedSinks()
    {
        var sink = new TestLogSink();
        var factory = new LoggerFactory(LogLevel.Debug);
        factory.AddSink(sink);

        var logger1 = factory.CreateLogger("CategoryA");
        var logger2 = factory.CreateLogger<LoggingTests>();

        logger1.LogDebug("debug A");
        logger2.LogInfo("info B");

        Assert.Equal(2, sink.Entries.Count);
        Assert.Equal("CategoryA", sink.Entries[0].Category);
        Assert.Equal("debug A", sink.Entries[0].Message);
        Assert.Equal("LoggingTests", sink.Entries[1].Category);
        Assert.Equal("info B", sink.Entries[1].Message);
    }

    [Fact]
    public void NullLogger_DoesNotThrow_AndReportsDisabled()
    {
        var logger = NullLogger.Instance;

        Assert.False(logger.IsEnabled(LogLevel.Error));
        logger.LogTrace("trace");
        logger.LogDebug("debug");
        logger.LogInfo("info");
        logger.LogWarning("warning");
        logger.LogError("error", new Exception("test"));
    }

    [Fact]
    public void FileLogSink_WritesFormattedLogAndException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "VoxelKing_Tests_" + Guid.NewGuid().ToString("N"));
        string logPath = Path.Combine(tempDir, "subfolder", "test.log");

        try
        {
            using (var sink = new FileLogSink(logPath, append: false))
            {
                sink.Emit(LogLevel.Info, "Core", "System initialized");
                sink.Emit(LogLevel.Error, "Core", "Failure occurred", new InvalidOperationException("Something failed"));
            }

            Assert.True(File.Exists(logPath));
            string contents = File.ReadAllText(logPath);

            Assert.Contains("[INFO ] [Core] System initialized", contents);
            Assert.Contains("[ERROR] [Core] Failure occurred", contents);
            Assert.Contains("InvalidOperationException: Something failed", contents);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FileLogSink_HandlesConcurrentWritesSafely()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "VoxelKing_Concurrent_" + Guid.NewGuid().ToString("N") + ".log");

        try
        {
            using (var sink = new FileLogSink(tempFile, append: false))
            {
                const int taskCount = 5;
                const int logsPerTask = 50;

                var tasks = new List<Task>();
                for (int t = 0; t < taskCount; t++)
                {
                    int taskId = t;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = 0; i < logsPerTask; i++)
                        {
                            sink.Emit(LogLevel.Info, $"Task{taskId}", $"Message {i}");
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            var lines = File.ReadAllLines(tempFile);
            Assert.Equal(250, lines.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
