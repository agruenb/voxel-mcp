namespace VoxelKing.Core.Common;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

/// <summary>
/// Thread-safe dispatcher executing queued actions on the main thread within a configurable per-frame time budget.
/// </summary>
public sealed class MainThreadDispatcher : IMainThreadDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Event raised when an unhandled exception occurs while executing an enqueued action.
    /// </summary>
    public event Action<Exception>? UnhandledException;

    /// <summary>
    /// Gets the current number of pending actions in the queue.
    /// </summary>
    public int PendingCount => _queue.Count;

    public MainThreadDispatcher(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
    }

    public Task EnqueueAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task<T> EnqueueAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() =>
        {
            try
            {
                var result = function();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public int Drain(TimeSpan? maxDuration = null)
    {
        if (maxDuration.HasValue && maxDuration.Value <= TimeSpan.Zero)
        {
            return 0;
        }

        var sw = maxDuration.HasValue ? Stopwatch.StartNew() : null;
        int executed = 0;
        int maxItemsToDrain = maxDuration.HasValue ? int.MaxValue : _queue.Count;

        while (executed < maxItemsToDrain && _queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                UnhandledException?.Invoke(ex);
                _logger?.LogError($"Unhandled exception in dispatcher action: {ex.Message}", ex);
            }

            executed++;

            if (sw != null && sw.Elapsed >= maxDuration!.Value)
            {
                break;
            }
        }

        return executed;
    }
}
