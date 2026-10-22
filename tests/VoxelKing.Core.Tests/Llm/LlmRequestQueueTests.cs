namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using Xunit;

public class LlmRequestQueueTests
{
    private static LlmRequest CreateSampleRequest(string prompt = "build a stone wall")
    {
        return new LlmRequest("system instruction", prompt);
    }

    private static LlmResponse CreateSampleSuccessResponse(int promptTokens = 50, int outputTokens = 25)
    {
        var brain = new LlmBrainResponse("Sure, I will build that wall.", false, false, "Building stone wall", "function main() end");
        return LlmResponse.Success(brain, "{\"reply\":\"Sure...\"}", new LlmUsage(promptTokens, outputTokens, promptTokens + outputTokens));
    }

    private sealed class TestLlmClient : ILlmClient
    {
        private readonly Func<LlmRequest, CancellationToken, Task<LlmResponse>> _handler;
        private int _currentConcurrent;

        public int CallCount;
        public int MaxConcurrent;

        public TestLlmClient(Func<LlmRequest, CancellationToken, Task<LlmResponse>> handler)
        {
            _handler = handler;
        }

        public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            int concurrent = Interlocked.Increment(ref _currentConcurrent);
            lock (this)
            {
                if (concurrent > MaxConcurrent)
                {
                    MaxConcurrent = concurrent;
                }
            }
            Interlocked.Increment(ref CallCount);

            try
            {
                return await _handler(request, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrent);
            }
        }
    }

    [Fact]
    public async Task ConcurrencyLimit_RestrictsSimultaneousRequests()
    {
        var config = new LlmConfig
        {
            MaxConcurrentRequests = 2,
            RequestsPerMinute = 0
        };

        var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var testClient = new TestLlmClient(async (_, ct) =>
        {
            using var reg = ct.Register(() => releaseTcs.TrySetCanceled(ct));
            await releaseTcs.Task;
            return CreateSampleSuccessResponse();
        });

        using var queue = new LlmRequestQueue(testClient, config);

        var tasks = new List<Task<LlmResponse>>();
        for (int i = 1; i <= 5; i++)
        {
            tasks.Add(queue.EnqueueAsync(new EntityId(i), CreateSampleRequest()));
        }

        // Give tasks time to queue up and hit the semaphore
        await Task.Delay(50);

        Assert.Equal(2, testClient.MaxConcurrent);
        Assert.Equal(2, queue.UsageStats.InFlightRequests);
        Assert.Equal(3, queue.UsageStats.QueuedRequests);

        // Release all
        releaseTcs.SetResult(true);
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.True(r.IsSuccess));
        Assert.Equal(5, testClient.CallCount);
        Assert.Equal(2, testClient.MaxConcurrent);
        Assert.Equal(0, queue.UsageStats.InFlightRequests);
        Assert.Equal(0, queue.UsageStats.QueuedRequests);
    }

    [Fact]
    public async Task PerNpcCancellation_CancelsOlderInFlightRequestForSameNpc()
    {
        var config = new LlmConfig
        {
            MaxConcurrentRequests = 5,
            RequestsPerMinute = 0
        };

        var firstRequestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var testClient = new TestLlmClient(async (req, ct) =>
        {
            if (req.Messages[0].Text == "instruction-1")
            {
                firstRequestStarted.TrySetResult(true);
                await Task.Delay(10000, ct); // Will be cancelled
                return CreateSampleSuccessResponse();
            }

            return CreateSampleSuccessResponse();
        });

        using var queue = new LlmRequestQueue(testClient, config);

        var npcId = new EntityId(42);

        var task1 = queue.EnqueueAsync(npcId, "Bob", CreateSampleRequest("instruction-1"));
        await firstRequestStarted.Task;

        // Enqueue a second instruction for the same NPC
        var task2 = queue.EnqueueAsync(npcId, "Bob", CreateSampleRequest("instruction-2"));

        var response1 = await task1;
        var response2 = await task2;

        Assert.False(response1.IsSuccess);
        Assert.Equal(LlmErrorKind.Cancelled, response1.Error?.Kind);

        Assert.True(response2.IsSuccess);
        Assert.Equal(1, queue.UsageStats.CancelledRequests);
        Assert.Equal(1, queue.UsageStats.SuccessfulRequests);
    }

    [Fact]
    public async Task PerNpcCancellation_DoesNotAffectDifferentNpcs()
    {
        var config = new LlmConfig
        {
            MaxConcurrentRequests = 5,
            RequestsPerMinute = 0
        };

        var npc1Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishAll = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var testClient = new TestLlmClient(async (req, ct) =>
        {
            if (req.Messages[0].Text == "npc1-first")
            {
                npc1Started.TrySetResult(true);
                using var reg = ct.Register(() => { });
                await Task.Delay(10000, ct); // cancelled by npc1-second
                return CreateSampleSuccessResponse();
            }

            using var reg2 = ct.Register(() => finishAll.TrySetCanceled(ct));
            await finishAll.Task;
            return CreateSampleSuccessResponse();
        });

        using var queue = new LlmRequestQueue(testClient, config);

        var npc1 = new EntityId(1);
        var npc2 = new EntityId(2);

        var task1 = queue.EnqueueAsync(npc1, CreateSampleRequest("npc1-first"));
        await npc1Started.Task;

        var task2 = queue.EnqueueAsync(npc2, CreateSampleRequest("npc2-first"));
        await Task.Delay(20);

        // Cancel npc1 with newer request
        var task3 = queue.EnqueueAsync(npc1, CreateSampleRequest("npc1-second"));

        var response1 = await task1;
        Assert.False(response1.IsSuccess);
        Assert.Equal(LlmErrorKind.Cancelled, response1.Error?.Kind);

        // Let remaining finish
        finishAll.SetResult(true);

        var response2 = await task2;
        var response3 = await task3;

        Assert.True(response2.IsSuccess);
        Assert.True(response3.IsSuccess);
    }

    [Fact]
    public async Task ExplicitCancel_CancelsInFlightNpcRequest()
    {
        var config = new LlmConfig { RequestsPerMinute = 0 };
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var testClient = new TestLlmClient(async (_, ct) =>
        {
            started.TrySetResult(true);
            await Task.Delay(10000, ct);
            return CreateSampleSuccessResponse();
        });

        using var queue = new LlmRequestQueue(testClient, config);
        var npcId = new EntityId(10);

        var task = queue.EnqueueAsync(npcId, CreateSampleRequest());
        await started.Task;

        bool cancelled = queue.Cancel(npcId);
        Assert.True(cancelled);

        var response = await task;
        Assert.False(response.IsSuccess);
        Assert.Equal(LlmErrorKind.Cancelled, response.Error?.Kind);
    }

    [Fact]
    public async Task CancelAll_CancelsAllActiveRequests()
    {
        var config = new LlmConfig { RequestsPerMinute = 0 };
        var startedCount = 0;
        var allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var testClient = new TestLlmClient(async (_, ct) =>
        {
            if (Interlocked.Increment(ref startedCount) == 2)
            {
                allStarted.TrySetResult(true);
            }
            await Task.Delay(10000, ct);
            return CreateSampleSuccessResponse();
        });

        using var queue = new LlmRequestQueue(testClient, config);
        var task1 = queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());
        var task2 = queue.EnqueueAsync(new EntityId(2), CreateSampleRequest());

        await allStarted.Task;

        queue.CancelAll();

        var response1 = await task1;
        var response2 = await task2;

        Assert.False(response1.IsSuccess);
        Assert.False(response2.IsSuccess);
        Assert.Equal(LlmErrorKind.Cancelled, response1.Error?.Kind);
        Assert.Equal(LlmErrorKind.Cancelled, response2.Error?.Kind);
    }

    [Fact]
    public async Task Retry_RetriesOnServerErrorAndSucceeds()
    {
        var config = new LlmConfig
        {
            MaxRetries = 2,
            RequestsPerMinute = 0
        };

        int calls = 0;
        var testClient = new TestLlmClient((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(LlmResponse.Failure(LlmError.Server("500 Internal Server Error", 500)));
            }

            return Task.FromResult(CreateSampleSuccessResponse());
        });

        var recordedDelays = new List<TimeSpan>();
        using var queue = new LlmRequestQueue(
            testClient,
            config,
            delayFunc: (span, _) => { recordedDelays.Add(span); return Task.CompletedTask; },
            initialBackoff: TimeSpan.FromMilliseconds(100));

        var response = await queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(2, calls);
        Assert.Single(recordedDelays);
        Assert.Equal(100, recordedDelays[0].TotalMilliseconds);
        Assert.Equal(1, queue.UsageStats.RetriedRequests);
        Assert.Equal(1, queue.UsageStats.SuccessfulRequests);
    }

    [Fact]
    public async Task Retry_RetriesOnRateLimited429AndSucceeds()
    {
        var config = new LlmConfig
        {
            MaxRetries = 2,
            RequestsPerMinute = 0
        };

        int calls = 0;
        var testClient = new TestLlmClient((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(LlmResponse.Failure(LlmError.RateLimited("429 Too Many Requests", 429)));
            }

            return Task.FromResult(CreateSampleSuccessResponse());
        });

        using var queue = new LlmRequestQueue(
            testClient,
            config,
            delayFunc: (_, _) => Task.CompletedTask,
            initialBackoff: TimeSpan.FromMilliseconds(1));

        var response = await queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(2, calls);
        Assert.Equal(1, queue.UsageStats.RetriedRequests);
    }

    [Fact]
    public async Task Retry_ExhaustsMaxRetries_ReturnsLastFailure()
    {
        var config = new LlmConfig
        {
            MaxRetries = 2,
            RequestsPerMinute = 0
        };

        int calls = 0;
        var testClient = new TestLlmClient((_, _) =>
        {
            calls++;
            return Task.FromResult(LlmResponse.Failure(LlmError.Server("503 Service Unavailable", 503)));
        });

        var recordedDelays = new List<TimeSpan>();
        using var queue = new LlmRequestQueue(
            testClient,
            config,
            delayFunc: (span, _) => { recordedDelays.Add(span); return Task.CompletedTask; },
            initialBackoff: TimeSpan.FromMilliseconds(50));

        var response = await queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(503, response.Error?.StatusCode);
        Assert.Equal(3, calls); // 1 initial + 2 retries
        Assert.Equal(2, recordedDelays.Count);
        Assert.Equal(50, recordedDelays[0].TotalMilliseconds);
        Assert.Equal(100, recordedDelays[1].TotalMilliseconds);
        Assert.Equal(2, queue.UsageStats.RetriedRequests);
        Assert.Equal(1, queue.UsageStats.FailedRequests);
    }

    [Fact]
    public async Task Retry_NonRetryableError_FailsImmediatelyWithoutRetrying()
    {
        var config = new LlmConfig
        {
            MaxRetries = 2,
            RequestsPerMinute = 0
        };

        int calls = 0;
        var testClient = new TestLlmClient((_, _) =>
        {
            calls++;
            return Task.FromResult(LlmResponse.Failure(LlmError.BadRequest("Bad Request", 400)));
        });

        using var queue = new LlmRequestQueue(testClient, config, delayFunc: (_, _) => Task.CompletedTask);

        var response = await queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.Error?.StatusCode);
        Assert.Equal(1, calls); // Only 1 attempt, no retries
        Assert.Equal(0, queue.UsageStats.RetriedRequests);
        Assert.Equal(1, queue.UsageStats.FailedRequests);
    }

    [Fact]
    public async Task Timeout_RequestTimesOut_FailsWithTimeoutError()
    {
        var config = new LlmConfig
        {
            TimeoutSeconds = 1,
            MaxRetries = 0,
            RequestsPerMinute = 0
        };

        var testClient = new TestLlmClient(async (_, ct) =>
        {
            await Task.Delay(5000, ct);
            return CreateSampleSuccessResponse();
        });

        using var queue = new LlmRequestQueue(testClient, config);

        var response = await queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(LlmErrorKind.Timeout, response.Error?.Kind);
    }

    [Fact]
    public async Task RateLimiter_ThrottlesWhenTokensExhausted()
    {
        var config = new LlmConfig
        {
            RequestsPerMinute = 2,
            MaxRetries = 0
        };

        var recordedDelays = new List<TimeSpan>();
        var testClient = new TestLlmClient((_, _) => Task.FromResult(CreateSampleSuccessResponse()));

        using var queue = new LlmRequestQueue(
            testClient,
            config,
            delayFunc: (span, _) => { recordedDelays.Add(span); return Task.CompletedTask; });

        // Request 1 and 2 consume the 2 available tokens immediately
        var res1 = await queue.EnqueueAsync(new EntityId(1), CreateSampleRequest());
        var res2 = await queue.EnqueueAsync(new EntityId(2), CreateSampleRequest());

        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);
        Assert.Empty(recordedDelays);

        // Request 3 has to wait ~30 seconds (2 req / min => 30s per token)
        var res3 = await queue.EnqueueAsync(new EntityId(3), CreateSampleRequest());

        Assert.True(res3.IsSuccess);
        Assert.Single(recordedDelays);
        Assert.InRange(recordedDelays[0].TotalSeconds, 29.0, 31.0);
    }

    [Fact]
    public async Task UsageStats_AccuratelyTracksTokensAndCounts()
    {
        var config = new LlmConfig { RequestsPerMinute = 0 };
        var testClient = new TestLlmClient((_, _) => Task.FromResult(CreateSampleSuccessResponse(promptTokens: 120, outputTokens: 80)));

        using var queue = new LlmRequestQueue(testClient, config);

        var response = await queue.EnqueueAsync(new EntityId(5), CreateSampleRequest());
        Assert.True(response.IsSuccess);

        var snapshot = queue.UsageStats.GetSnapshot();
        Assert.Equal(1, snapshot.TotalRequests);
        Assert.Equal(1, snapshot.CompletedRequests);
        Assert.Equal(1, snapshot.SuccessfulRequests);
        Assert.Equal(0, snapshot.FailedRequests);
        Assert.Equal(0, snapshot.CancelledRequests);
        Assert.Equal(120, snapshot.TotalPromptTokens);
        Assert.Equal(80, snapshot.TotalOutputTokens);
        Assert.Equal(200, snapshot.TotalTokens);
    }

    [Fact]
    public async Task FileLogging_WritesRequestResponseJsonToDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "voxel_test_llm_logs_" + Guid.NewGuid().ToString("N"));

        try
        {
            var config = new LlmConfig { RequestsPerMinute = 0 };
            var testClient = new TestLlmClient((_, _) => Task.FromResult(CreateSampleSuccessResponse(promptTokens: 30, outputTokens: 20)));

            var fileLogger = new FileLlmRequestLogger(tempDir);
            using var queue = new LlmRequestQueue(testClient, config, requestLogger: fileLogger);

            var npcId = new EntityId(99);
            var response = await queue.EnqueueAsync(npcId, "Bob the Farmer", CreateSampleRequest("chop trees"));

            Assert.True(response.IsSuccess);
            Assert.True(Directory.Exists(tempDir));

            var files = Directory.GetFiles(tempDir, "*Bob_the_Farmer.json");
            Assert.Single(files);

            string json = await File.ReadAllTextAsync(files[0]);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("Bob the Farmer", root.GetProperty("npcName").GetString());
            Assert.Equal(99, root.GetProperty("npcId").GetInt32());
            Assert.True(root.GetProperty("isSuccess").GetBoolean());
            Assert.True(root.TryGetProperty("request", out var reqEl));
            Assert.True(root.TryGetProperty("response", out var respEl));
            Assert.Equal(50, respEl.GetProperty("usage").GetProperty("totalTokens").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ILlmClient_GenerateAsync_RoutesThroughQueue()
    {
        var config = new LlmConfig { RequestsPerMinute = 0 };
        var testClient = new TestLlmClient((_, _) => Task.FromResult(CreateSampleSuccessResponse()));

        using var queue = new LlmRequestQueue(testClient, config);

        ILlmClient client = queue;
        var response = await client.GenerateAsync(CreateSampleRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(1, testClient.CallCount);
        Assert.Equal(1, queue.UsageStats.SuccessfulRequests);
    }
}
