namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using Xunit;

public class FallbackLlmClientTests
{
    [Fact]
    public async Task GenerateAsync_NoApiKey_AutomaticallyFallsBackToMockWithHudWarning()
    {
        var eventBus = new EventBus();
        var warningEvents = new List<LlmWarningEvent>();
        var spokeEvents = new List<EntitySpoke>();

        eventBus.Subscribe<LlmWarningEvent>(warningEvents.Add);
        eventBus.Subscribe<EntitySpoke>(spokeEvents.Add);

        var config = new LlmConfig { ApiKey = "" }; // Missing key
        var mockClient = new MockLlmClient();
        var dummyHttp = new HttpClient(new MockHttpMessageHandler());
        var primaryClient = new GeminiClient(dummyHttp, config);

        var fallbackClient = new FallbackLlmClient(primaryClient, mockClient, config, eventBus);

        Assert.True(fallbackClient.IsUsingMock);

        var request = new LlmRequest("sys", "collect 10 logs");
        var response = await fallbackClient.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.Contains("chop down trees", response.Content.Reply);

        // Verify warning broadcast
        Assert.Single(warningEvents);
        Assert.Contains("No Gemini API key configured", warningEvents[0].Message);

        Assert.Single(spokeEvents);
        Assert.Equal("System", spokeEvents[0].SpeakerName);
        Assert.Contains("No Gemini API key configured", spokeEvents[0].Text);

        // Second call should not spam duplicate warnings
        await fallbackClient.GenerateAsync(request);
        Assert.Single(warningEvents);
        Assert.Single(spokeEvents);
    }

    [Fact]
    public async Task GenerateAsync_WithValidApiKey_CallsPrimaryWithoutWarning()
    {
        var eventBus = new EventBus();
        var warningEvents = new List<LlmWarningEvent>();
        eventBus.Subscribe<LlmWarningEvent>(warningEvents.Add);

        string successJson = @"
        {
          ""candidates"": [
            {
              ""content"": {
                ""parts"": [
                  {
                    ""text"": ""{\n  \""reply\"": \""Real Gemini answer\"",\n  \""needs_clarification\"": false,\n  \""keep_current_task\"": false,\n  \""task_summary\"": \""Task\"",\n  \""script\"": \""function main() end\""\n}""
                  }
                ],
                ""role"": ""model""
              }
            }
          ]
        }";

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, successJson);
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "valid-gemini-key" };
        var primaryClient = new GeminiClient(httpClient, config);
        var mockClient = new MockLlmClient();

        var fallbackClient = new FallbackLlmClient(primaryClient, mockClient, config, eventBus);

        Assert.False(fallbackClient.IsUsingMock);

        var request = new LlmRequest("sys", "do something");
        var response = await fallbackClient.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.Equal("Real Gemini answer", response.Content?.Reply);
        Assert.Empty(warningEvents);
    }

    [Fact]
    public async Task GenerateAsync_RejectedApiKey_WarnsAndFallsBackToMock()
    {
        var eventBus = new EventBus();
        var warningEvents = new List<LlmWarningEvent>();
        eventBus.Subscribe<LlmWarningEvent>(warningEvents.Add);

        string invalidKeyJson = @"
        {
          ""error"": {
            ""code"": 400,
            ""message"": ""API key not valid."",
            ""status"": ""API_KEY_INVALID""
          }
        }";

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, invalidKeyJson);
        using var httpClient = new HttpClient(handler);
        var config = new LlmConfig { ApiKey = "expired-or-bad-key" };
        var primaryClient = new GeminiClient(httpClient, config);
        var mockClient = new MockLlmClient();

        var fallbackClient = new FallbackLlmClient(primaryClient, mockClient, config, eventBus);

        var request = new LlmRequest("sys", "gather logs");
        var response = await fallbackClient.GenerateAsync(request);

        // Fallback occurred
        Assert.True(fallbackClient.IsUsingMock);
        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.Contains("chop down trees", response.Content.Reply);

        Assert.Single(warningEvents);
        Assert.Contains("rejected", warningEvents[0].Message);
    }
}
