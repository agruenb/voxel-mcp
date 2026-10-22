namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Llm;
using Xunit;

public class GeminiClientTests
{
    private const string SampleSuccessFixture = @"
    {
      ""candidates"": [
        {
          ""content"": {
            ""parts"": [
              {
                ""text"": ""{\n  \""reply\"": \""Alright, I'll chop down trees for you.\"",\n  \""needs_clarification\"": false,\n  \""keep_current_task\"": false,\n  \""task_summary\"": \""Gather 10 oak logs\"",\n  \""script\"": \""function main()\\n  npc.say('Chopping')\\nend\""\n}""
              }
            ],
            ""role"": ""model""
          },
          ""finishReason"": ""STOP"",
          ""index"": 0
        }
      ],
      ""usageMetadata"": {
        ""promptTokenCount"": 450,
        ""candidatesTokenCount"": 85,
        ""totalTokenCount"": 535
      },
      ""modelVersion"": ""gemini-2.5-flash""
    }";

    private const string SampleRateLimitFixture = @"
    {
      ""error"": {
        ""code"": 429,
        ""message"": ""Resource has been exhausted (e.g. check quota)."",
        ""status"": ""RESOURCE_EXHAUSTED""
      }
    }";

    private const string SampleInvalidApiKeyFixture = @"
    {
      ""error"": {
        ""code"": 400,
        ""message"": ""API key not valid. Please pass a valid API key."",
        ""status"": ""API_KEY_INVALID""
      }
    }";

    private const string SampleServerErrorFixture = @"
    {
      ""error"": {
        ""code"": 503,
        ""message"": ""The model is overloaded. Please try again later."",
        ""status"": ""UNAVAILABLE""
      }
    }";

    [Fact]
    public async Task GenerateAsync_SuccessfulResponse_ParsesContentAndUsage()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, SampleSuccessFixture);
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "test-key-123" };
        var client = new GeminiClient(httpClient, config);

        var request = new LlmRequest("System instructions", "Chop some trees");
        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.Null(response.Error);
        Assert.NotNull(response.Content);
        Assert.Equal("Alright, I'll chop down trees for you.", response.Content.Reply);
        Assert.False(response.Content.NeedsClarification);
        Assert.False(response.Content.KeepCurrentTask);
        Assert.Equal("Gather 10 oak logs", response.Content.TaskSummary);
        Assert.Contains("function main()", response.Content.Script);

        Assert.Equal(450, response.Usage.PromptTokens);
        Assert.Equal(85, response.Usage.OutputTokens);
        Assert.Equal(535, response.Usage.TotalTokens);

        // Verify request headers and URL
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent", handler.LastRequest.RequestUri?.ToString());
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-goog-api-key", out var keyValues));
        Assert.Contains("test-key-123", keyValues);

        // Verify request body schema
        Assert.NotNull(handler.LastRequestBody);
        using var requestDoc = JsonDocument.Parse(handler.LastRequestBody);
        var root = requestDoc.RootElement;
        Assert.True(root.TryGetProperty("system_instruction", out _));
        Assert.True(root.TryGetProperty("contents", out _));
        Assert.True(root.TryGetProperty("generationConfig", out var genConfig));
        Assert.Equal("application/json", genConfig.GetProperty("responseMimeType").GetString());
        Assert.True(genConfig.TryGetProperty("responseSchema", out _));
    }

    [Fact]
    public async Task GenerateAsync_RateLimit429_MapsToRetryableError()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, SampleRateLimitFixture);
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "test-key" };
        var client = new GeminiClient(httpClient, config);

        var request = new LlmRequest("sys", "user");
        var response = await client.GenerateAsync(request);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Error);
        Assert.Equal(LlmErrorKind.RateLimited, response.Error.Kind);
        Assert.Equal(429, response.Error.StatusCode);
        Assert.True(response.Error.IsRetryable);
        Assert.Contains("Resource has been exhausted", response.Error.Message);
    }

    [Fact]
    public async Task GenerateAsync_InvalidApiKey_MapsToNonRetryableInvalidApiKeyError()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, SampleInvalidApiKeyFixture);
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "bad-key" };
        var client = new GeminiClient(httpClient, config);

        var request = new LlmRequest("sys", "user");
        var response = await client.GenerateAsync(request);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Error);
        Assert.Equal(LlmErrorKind.InvalidApiKey, response.Error.Kind);
        Assert.False(response.Error.IsRetryable);
    }

    [Fact]
    public async Task GenerateAsync_ServerError503_MapsToRetryableServerError()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable, SampleServerErrorFixture);
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "test-key" };
        var client = new GeminiClient(httpClient, config);

        var request = new LlmRequest("sys", "user");
        var response = await client.GenerateAsync(request);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Error);
        Assert.Equal(LlmErrorKind.ServerError, response.Error.Kind);
        Assert.Equal(503, response.Error.StatusCode);
        Assert.True(response.Error.IsRetryable);
    }

    [Fact]
    public async Task GenerateAsync_NetworkFailure_MapsToRetryableNetworkError()
    {
        var handler = new MockHttpMessageHandler
        {
            ExceptionToThrow = new HttpRequestException("DNS resolution failed")
        };
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "test-key" };
        var client = new GeminiClient(httpClient, config);

        var request = new LlmRequest("sys", "user");
        var response = await client.GenerateAsync(request);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Error);
        Assert.Equal(LlmErrorKind.NetworkError, response.Error.Kind);
        Assert.True(response.Error.IsRetryable);
    }

    [Fact]
    public async Task GenerateAsync_Cancellation_ReturnsCancelledError()
    {
        var handler = new MockHttpMessageHandler { Delay = TimeSpan.FromSeconds(5) };
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "test-key" };
        var client = new GeminiClient(httpClient, config);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var request = new LlmRequest("sys", "user");
        var response = await client.GenerateAsync(request, cts.Token);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Error);
        Assert.Equal(LlmErrorKind.Cancelled, response.Error.Kind);
    }

    [Fact]
    public async Task SmokeTest_AgainstLiveGemini_WhenApiKeyAvailable()
    {
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Skip smoke test gracefully when no live key is configured in test environment
            return;
        }

        using var httpClient = new HttpClient();
        var config = new LlmConfig { ApiKey = apiKey };
        var client = new GeminiClient(httpClient, config);

        var promptBuilder = new PromptBuilder();
        var prompt = promptBuilder.BuildPrompt(
            new NpcPromptContext
            {
                Name = "Bob",
                Personality = "helpful builder",
                Instruction = "Collect 5 oak logs"
            }
        );

        var request = new LlmRequest(prompt);
        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess, $"Live Gemini call failed: {response.Error?.Message}");
        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content.Reply);
    }
}
