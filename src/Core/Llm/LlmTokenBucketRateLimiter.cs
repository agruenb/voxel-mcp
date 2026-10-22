namespace VoxelKing.Core.Llm;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Thread-safe token bucket rate limiter enforcing a maximum number of requests per minute (F-403).
/// Allows bursts up to the bucket capacity while smoothing sustained request throughput.
/// </summary>
public sealed class LlmTokenBucketRateLimiter
{
    private readonly object _lock = new();
    private readonly Func<TimeSpan, CancellationToken, Task> _delayFunc;
    private readonly Func<DateTimeOffset> _timeProvider;
    private readonly double _refillRatePerSecond;

    private double _availableTokens;
    private DateTimeOffset _lastRefill;

    /// <summary>
    /// Gets the configured requests per minute ceiling.
    /// Values less than or equal to 0 disable rate limiting.
    /// </summary>
    public int RequestsPerMinute { get; }

    /// <summary>
    /// Gets the current estimated number of available tokens in the bucket.
    /// </summary>
    public double AvailableTokens
    {
        get
        {
            lock (_lock)
            {
                Refill();
                return _availableTokens;
            }
        }
    }

    public LlmTokenBucketRateLimiter(
        int requestsPerMinute,
        Func<TimeSpan, CancellationToken, Task>? delayFunc = null,
        Func<DateTimeOffset>? timeProvider = null)
    {
        RequestsPerMinute = requestsPerMinute;
        _delayFunc = delayFunc ?? Task.Delay;
        _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow);

        _refillRatePerSecond = requestsPerMinute > 0 ? (double)requestsPerMinute / 60.0 : 0.0;
        _availableTokens = requestsPerMinute > 0 ? requestsPerMinute : 0.0;
        _lastRefill = _timeProvider();
    }

    /// <summary>
    /// Waits asynchronously until a token is available to proceed, respecting rate limits and cancellation.
    /// </summary>
    public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        if (RequestsPerMinute <= 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        TimeSpan waitTime;
        lock (_lock)
        {
            Refill();

            if (_availableTokens >= 1.0)
            {
                _availableTokens -= 1.0;
                return;
            }

            double missing = 1.0 - _availableTokens;
            double waitSeconds = missing / _refillRatePerSecond;
            _availableTokens -= 1.0; // Reserve the slot to guarantee sequential spacing
            waitTime = TimeSpan.FromSeconds(waitSeconds);
        }

        if (waitTime > TimeSpan.Zero)
        {
            try
            {
                await _delayFunc(waitTime, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Refund token if caller was cancelled while waiting
                lock (_lock)
                {
                    _availableTokens = Math.Min(RequestsPerMinute, _availableTokens + 1.0);
                }
                throw;
            }
        }
    }

    private void Refill()
    {
        var now = _timeProvider();
        double elapsedSeconds = (now - _lastRefill).TotalSeconds;
        if (elapsedSeconds > 0)
        {
            _availableTokens = Math.Min(RequestsPerMinute, _availableTokens + (elapsedSeconds * _refillRatePerSecond));
            _lastRefill = now;
        }
    }
}
