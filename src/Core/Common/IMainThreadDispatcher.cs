namespace VoxelKing.Core.Common;

using System;
using System.Threading.Tasks;

/// <summary>
/// Thread-safe queue for marshalling callbacks and work items to the main simulation thread.
/// </summary>
public interface IMainThreadDispatcher
{
    /// <summary>
    /// The number of queued actions currently pending execution.
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Enqueues an action to be executed on the main thread (fire-and-forget).
    /// </summary>
    /// <param name="action">The action to execute.</param>
    void Enqueue(Action action);

    /// <summary>
    /// Enqueues an asynchronous action returning a Task that completes when the action has executed on the main thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A Task representing the execution on the main thread.</returns>
    Task EnqueueAsync(Action action);

    /// <summary>
    /// Enqueues an asynchronous function returning a Task that completes with the result when executed on the main thread.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <returns>A Task containing the result computed on the main thread.</returns>
    Task<T> EnqueueAsync<T>(Func<T> function);

    /// <summary>
    /// Drains queued actions up to the specified time budget or until the queue is empty.
    /// </summary>
    /// <param name="maxDuration">Optional maximum time to spend draining the queue.</param>
    /// <returns>The number of actions executed during this drain invocation.</returns>
    int Drain(TimeSpan? maxDuration = null);
}
