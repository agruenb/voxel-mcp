namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Threading.Tasks;
using VoxelKing.Core.Llm;
using Xunit;

public class MockLlmClientTests
{
    [Theory]
    [InlineData("Please gather 10 oak logs for me", "oak_log")]
    [InlineData("Follow me closely", "player")]
    [InlineData("Build a stone wall around the crops", "wall")]
    [InlineData("Guard this perimeter from monsters", "guard")]
    [InlineData("Harvest the wheat farm", "wheat")]
    [InlineData("Go home when night falls", "home")]
    [InlineData("Deliver these items to my chest", "chest")]
    public async Task GenerateAsync_KeywordMapping_SelectsExpectedBrain(string instruction, string expectedScriptSnippet)
    {
        var client = new MockLlmClient();
        var request = new LlmRequest("system", instruction);

        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.False(response.Content.NeedsClarification);
        Assert.False(response.Content.KeepCurrentTask);
        Assert.NotEmpty(response.Content.Reply);
        Assert.NotEmpty(response.Content.TaskSummary);
        Assert.Contains(expectedScriptSnippet, response.Content.Script);
        Assert.Contains("function main()", response.Content.Script);
    }

    [Theory]
    [InlineData("run test_syntax_error please", "Missing closing parameter")]
    [InlineData("run test_unknown_api please", "teleport_player_to_orbit")]
    [InlineData("run test_busy_loop please", "while true do")]
    [InlineData("run test_blocking_handler please", "npc.go_to")]
    public async Task GenerateAsync_InvalidScriptTriggers_ReturnsExpectedBrokenBrains(string instruction, string expectedScriptSnippet)
    {
        var client = new MockLlmClient();
        var request = new LlmRequest("system", instruction);

        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content.Script);
        Assert.Contains(expectedScriptSnippet, response.Content.Script);
    }

    [Theory]
    [InlineData("Hello Bob!")]
    [InlineData("Hey there, how are you?")]
    [InlineData("Good morning friend")]
    public async Task GenerateAsync_ChitChat_ReturnsKeepCurrentTask(string greeting)
    {
        var client = new MockLlmClient();
        var request = new LlmRequest("system", greeting);

        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.True(response.Content.KeepCurrentTask);
        Assert.False(response.Content.NeedsClarification);
        Assert.Empty(response.Content.Script);
        Assert.NotEmpty(response.Content.Reply);
    }

    [Theory]
    [InlineData("What should I do?")]
    [InlineData("asdfghjkl")]
    [InlineData("Do that thing over there")]
    public async Task GenerateAsync_Ambiguous_ReturnsNeedsClarification(string ambiguousText)
    {
        var client = new MockLlmClient();
        var request = new LlmRequest("system", ambiguousText);

        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.True(response.Content.NeedsClarification);
        Assert.False(response.Content.KeepCurrentTask);
        Assert.Empty(response.Content.Script);
        Assert.NotEmpty(response.Content.Reply);
    }

    [Fact]
    public async Task GenerateAsync_SimulatedError_ReturnsFailure()
    {
        var client = new MockLlmClient
        {
            SimulatedError = LlmError.RateLimited("Simulated quota exceeded")
        };

        var request = new LlmRequest("system", "gather wood");
        var response = await client.GenerateAsync(request);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Error);
        Assert.Equal(LlmErrorKind.RateLimited, response.Error.Kind);
        Assert.Contains("Simulated quota exceeded", response.Error.Message);
    }

    [Fact]
    public async Task GenerateAsync_CustomHandler_OverridesBehavior()
    {
        var client = new MockLlmClient
        {
            CustomHandler = req => LlmResponse.Success(
                new LlmBrainResponse("Custom reply", taskSummary: "Custom task", script: "function main() end"),
                rawText: "{}",
                usage: new LlmUsage(10, 10, 20))
        };

        var request = new LlmRequest("system", "anything");
        var response = await client.GenerateAsync(request);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Content);
        Assert.Equal("Custom reply", response.Content.Reply);
        Assert.Equal("Custom task", response.Content.TaskSummary);
    }
}
