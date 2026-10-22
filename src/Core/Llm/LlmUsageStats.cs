namespace VoxelKing.Core.Llm;

using System.Threading;

/// <summary>
/// Immutable snapshot of LLM request counts and token consumption statistics (F-403).
/// </summary>
public readonly record struct LlmUsageStatsSnapshot(
    long TotalRequests,
    long CompletedRequests,
    long SuccessfulRequests,
    long FailedRequests,
    long CancelledRequests,
    long RetriedRequests,
    int QueuedRequests,
    int InFlightRequests,
    long TotalPromptTokens,
    long TotalOutputTokens,
    long TotalTokens);

/// <summary>
/// Thread-safe tracker for LLM queue activity and token consumption metrics (F-403).
/// Aggregated for display in the debug overlay and performance telemetry.
/// </summary>
public sealed class LlmUsageStats
{
    private long _totalRequests;
    private long _completedRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _cancelledRequests;
    private long _retriedRequests;
    private int _queuedRequests;
    private int _inFlightRequests;
    private long _totalPromptTokens;
    private long _totalOutputTokens;
    private long _totalTokens;

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long CompletedRequests => Interlocked.Read(ref _completedRequests);
    public long SuccessfulRequests => Interlocked.Read(ref _successfulRequests);
    public long FailedRequests => Interlocked.Read(ref _failedRequests);
    public long CancelledRequests => Interlocked.Read(ref _cancelledRequests);
    public long RetriedRequests => Interlocked.Read(ref _retriedRequests);
    public int QueuedRequests => Volatile.Read(ref _queuedRequests);
    public int InFlightRequests => Volatile.Read(ref _inFlightRequests);
    public long TotalPromptTokens => Interlocked.Read(ref _totalPromptTokens);
    public long TotalOutputTokens => Interlocked.Read(ref _totalOutputTokens);
    public long TotalTokens => Interlocked.Read(ref _totalTokens);

    public void RecordEnqueued()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _queuedRequests);
    }

    public void RecordInFlightStarted()
    {
        Interlocked.Decrement(ref _queuedRequests);
        Interlocked.Increment(ref _inFlightRequests);
    }

    public void RecordInFlightEnded()
    {
        Interlocked.Decrement(ref _inFlightRequests);
    }

    public void RecordQueueExitedBeforeInFlight()
    {
        Interlocked.Decrement(ref _queuedRequests);
    }

    public void RecordSuccess(LlmUsage usage)
    {
        Interlocked.Increment(ref _completedRequests);
        Interlocked.Increment(ref _successfulRequests);
        if (usage.PromptTokens > 0)
        {
            Interlocked.Add(ref _totalPromptTokens, usage.PromptTokens);
        }
        if (usage.OutputTokens > 0)
        {
            Interlocked.Add(ref _totalOutputTokens, usage.OutputTokens);
        }
        if (usage.TotalTokens > 0)
        {
            Interlocked.Add(ref _totalTokens, usage.TotalTokens);
        }
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _completedRequests);
        Interlocked.Increment(ref _failedRequests);
    }

    public void RecordCancelled()
    {
        Interlocked.Increment(ref _completedRequests);
        Interlocked.Increment(ref _cancelledRequests);
    }

    public void RecordRetry()
    {
        Interlocked.Increment(ref _retriedRequests);
    }

    public LlmUsageStatsSnapshot GetSnapshot() => new(
        TotalRequests,
        CompletedRequests,
        SuccessfulRequests,
        FailedRequests,
        CancelledRequests,
        RetriedRequests,
        QueuedRequests,
        InFlightRequests,
        TotalPromptTokens,
        TotalOutputTokens,
        TotalTokens);
}
