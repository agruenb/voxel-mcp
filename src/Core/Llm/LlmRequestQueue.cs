namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Thread-safe request queue coordinating LLM calls with global concurrency limits,
/// token-bucket rate limits, per-NPC cancellation, timeout, retry with backoff, usage stats, and file logging (F-403).
/// </summary>
public sealed class LlmRequestQueue : ILlmRequestQueue, ILlmClient, IDisposable
{
    private static readonly TimeSpan DefaultInitialBackoff = TimeSpan.FromSeconds(1.0);

    private readonly ILlmClient _innerClient;
    private readonly LlmConfig _config;
    private readonly ILlmRequestLogger? _requestLogger;
    private readonly ILogger? _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayFunc;
    private readonly Func<DateTimeOffset> _timeProvider;
    private readonly TimeSpan _initialBackoff;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly LlmTokenBucketRateLimiter _rateLimiter;
    private readonly LlmUsageStats _usageStats = new();

    private readonly object _activeNpcLock = new();
    private readonly Dictionary<EntityId, CancellationTokenSource> _activeNpcRequests = new();
    private bool _disposed;

    /// <inheritdoc/>
    public LlmUsageStats UsageStats => _usageStats;

    /// <summary>
    /// Gets the inner client wrapped by this queue.
    /// </summary>
    public ILlmClient InnerClient => _innerClient;

    /// <summary>
    /// Gets the active configuration used by this queue.
    /// </summary>
    public LlmConfig Config => _config;

    public LlmRequestQueue(
        ILlmClient innerClient,
        LlmConfig config,
        ILlmRequestLogger? requestLogger = null,
        ILogger? logger = null,
        Func<TimeSpan, CancellationToken, Task>? delayFunc = null,
        Func<DateTimeOffset>? timeProvider = null,
        TimeSpan? initialBackoff = null)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _requestLogger = requestLogger;
        _logger = logger;
        _delayFunc = delayFunc ?? Task.Delay;
        _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow);
        _initialBackoff = initialBackoff ?? DefaultInitialBackoff;

        int maxConcurrent = Math.Max(1, _config.MaxConcurrentRequests);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        _rateLimiter = new LlmTokenBucketRateLimiter(_config.RequestsPerMinute, _delayFunc, _timeProvider);
    }

    /// <inheritdoc/>
    public Task<LlmResponse> EnqueueAsync(
        EntityId npcId,
        LlmRequest request,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(npcId, npcId != EntityId.None ? $"Npc_{npcId.Value}" : "Global", request, cancellationToken);

    /// <inheritdoc/>
    public async Task<LlmResponse> EnqueueAsync(
        EntityId npcId,
        string npcName,
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(npcName))
        {
            npcName = npcId != EntityId.None ? $"Npc_{npcId.Value}" : "Global";
        }

        CancellationTokenSource? npcCts = null;

        // 1. Per-NPC cancellation: a new instruction to the same NPC cancels any older in-flight request
        if (npcId != EntityId.None)
        {
            npcCts = new CancellationTokenSource();
            lock (_activeNpcLock)
            {
                if (_disposed)
                {
                    npcCts.Dispose();
                    throw new ObjectDisposedException(nameof(LlmRequestQueue));
                }

                if (_activeNpcRequests.TryGetValue(npcId, out var existingCts))
                {
                    _logger?.LogDebug($"Cancelling previous in-flight LLM request for NPC {npcName} ({npcId}) due to newer instruction.");
                    try
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                    }
                    catch
                    {
                    }
                }

                _activeNpcRequests[npcId] = npcCts;
            }
        }

        using var linkedCts = npcCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, npcCts.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var linkedToken = linkedCts.Token;

        if (linkedToken.IsCancellationRequested)
        {
            CleanupActiveNpc(npcId, npcCts);
            _usageStats.RecordCancelled();
            return LlmResponse.Failure(LlmError.Cancelled("Request was cancelled before queueing."));
        }

        _usageStats.RecordEnqueued();
        var stopwatch = Stopwatch.StartNew();

        bool acquiredSemaphore = false;
        try
        {
            // 2. Rate limit: wait for a token bucket slot
            await _rateLimiter.WaitAsync(linkedToken).ConfigureAwait(false);

            // 3. Concurrency limit: acquire global execution slot
            await _concurrencySemaphore.WaitAsync(linkedToken).ConfigureAwait(false);
            acquiredSemaphore = true;
            _usageStats.RecordInFlightStarted();

            // 4. Execution loop with per-attempt timeout and exponential backoff retry
            int maxRetries = Math.Max(0, _config.MaxRetries);
            TimeSpan timeout = TimeSpan.FromSeconds(Math.Max(1, _config.TimeoutSeconds));
            LlmResponse? lastResponse = null;
            int attempt = 0;

            for (; attempt <= maxRetries; attempt++)
            {
                if (linkedToken.IsCancellationRequested)
                {
                    lastResponse = LlmResponse.Failure(LlmError.Cancelled("Request was cancelled."));
                    break;
                }

                if (attempt > 0)
                {
                    _usageStats.RecordRetry();
                    var backoff = _initialBackoff * Math.Pow(2, attempt - 1);
                    _logger?.LogDebug($"Retrying LLM request for NPC {npcName} (attempt {attempt + 1}/{maxRetries + 1}) after {backoff.TotalMilliseconds}ms backoff...");
                    try
                    {
                        await _delayFunc(backoff, linkedToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        lastResponse = LlmResponse.Failure(LlmError.Cancelled("Request was cancelled during retry backoff."));
                        break;
                    }
                }

                using var attemptTimeoutCts = new CancellationTokenSource(timeout);
                using var attemptLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken, attemptTimeoutCts.Token);

                try
                {
                    lastResponse = await _innerClient.GenerateAsync(request, attemptLinkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                {
                    lastResponse = LlmResponse.Failure(LlmError.Cancelled("Request was cancelled."));
                    break;
                }
                catch (OperationCanceledException) when (attemptTimeoutCts.IsCancellationRequested)
                {
                    lastResponse = LlmResponse.Failure(LlmError.Timeout($"Request timed out after {timeout.TotalSeconds} seconds."));
                }
                catch (Exception ex)
                {
                    lastResponse = LlmResponse.Failure(LlmError.Unknown($"Unexpected exception during LLM generation: {ex.Message}", ex));
                }

                // If the inner client caught the cancellation and returned Cancelled,
                // check if it was actually attemptTimeoutCts that fired rather than the caller's token
                if (lastResponse != null && !lastResponse.IsSuccess && lastResponse.Error?.Kind == LlmErrorKind.Cancelled &&
                    attemptTimeoutCts.IsCancellationRequested && !linkedToken.IsCancellationRequested)
                {
                    _logger?.LogWarning($"LLM request for NPC {npcName} timed out after {timeout.TotalSeconds} seconds.");
                    lastResponse = LlmResponse.Failure(LlmError.Timeout($"Request timed out after {timeout.TotalSeconds} seconds."));
                }

                if (lastResponse?.IsSuccess == true)
                {
                    break;
                }

                // If error is not transient/retryable, do not retry
                if (lastResponse?.Error == null || !lastResponse.Error.IsRetryable || attempt >= maxRetries)
                {
                    break;
                }
            }

            lastResponse ??= LlmResponse.Failure(LlmError.Unknown("No response produced."));

            stopwatch.Stop();
            double durationMs = stopwatch.Elapsed.TotalMilliseconds;

            // 5. Update usage stats
            if (lastResponse.IsSuccess)
            {
                _usageStats.RecordSuccess(lastResponse.Usage);
            }
            else if (lastResponse.Error?.Kind == LlmErrorKind.Cancelled)
            {
                _usageStats.RecordCancelled();
            }
            else
            {
                _usageStats.RecordFailure();
            }

            // 6. Audit logging
            if (_requestLogger != null)
            {
                var logEntry = new LlmQueueLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    NpcId = npcId != EntityId.None ? npcId.Value : null,
                    NpcName = npcName,
                    DurationMs = durationMs,
                    Attempts = attempt + 1,
                    IsSuccess = lastResponse.IsSuccess,
                    Request = request,
                    Response = lastResponse
                };

                try
                {
                    await _requestLogger.LogAsync(logEntry, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to record LLM queue audit log: {ex.Message}");
                }
            }

            return lastResponse;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _usageStats.RecordCancelled();
            var cancelledResponse = LlmResponse.Failure(LlmError.Cancelled("Request was cancelled while waiting in queue."));

            if (_requestLogger != null)
            {
                var logEntry = new LlmQueueLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    NpcId = npcId != EntityId.None ? npcId.Value : null,
                    NpcName = npcName,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                    Attempts = 0,
                    IsSuccess = false,
                    Request = request,
                    Response = cancelledResponse
                };

                try
                {
                    await _requestLogger.LogAsync(logEntry, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            return cancelledResponse;
        }
        finally
        {
            if (acquiredSemaphore)
            {
                _concurrencySemaphore.Release();
                _usageStats.RecordInFlightEnded();
            }
            else
            {
                _usageStats.RecordQueueExitedBeforeInFlight();
            }

            CleanupActiveNpc(npcId, npcCts);
        }
    }

    /// <inheritdoc/>
    public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
        EnqueueAsync(EntityId.None, "Global", request, cancellationToken);

    /// <inheritdoc/>
    public bool Cancel(EntityId npcId)
    {
        if (npcId == EntityId.None)
        {
            return false;
        }

        lock (_activeNpcLock)
        {
            if (_activeNpcRequests.TryGetValue(npcId, out var cts))
            {
                try
                {
                    cts.Cancel();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void CancelAll()
    {
        lock (_activeNpcLock)
        {
            foreach (var cts in _activeNpcRequests.Values)
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                }
            }
        }
    }

    private void CleanupActiveNpc(EntityId npcId, CancellationTokenSource? npcCts)
    {
        if (npcId == EntityId.None || npcCts == null)
        {
            return;
        }

        lock (_activeNpcLock)
        {
            if (_activeNpcRequests.TryGetValue(npcId, out var currentCts) && currentCts == npcCts)
            {
                _activeNpcRequests.Remove(npcId);
            }
        }

        try
        {
            npcCts.Dispose();
        }
        catch
        {
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_activeNpcLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var cts in _activeNpcRequests.Values)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch
                {
                }
            }
            _activeNpcRequests.Clear();
        }

        _concurrencySemaphore.Dispose();
    }
}
