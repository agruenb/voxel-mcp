namespace VoxelKing.Core.Tests.Llm;

using System;
using VoxelKing.Core.Llm;
using Xunit;

public class LlmResponseParserTests
{
    [Fact]
    public void TryParse_ValidJson_ReturnsPopulatedResponse()
    {
        string json = @"
        {
            ""reply"": ""Alright, I will go chop down trees and gather logs for you."",
            ""needs_clarification"": false,
            ""keep_current_task"": false,
            ""task_summary"": ""Gather 10 oak logs and deposit into chest"",
            ""script"": ""function main()\n  npc.say('Starting')\nend""
        }";

        bool ok = LlmResponseParser.TryParse(json, out var response, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(response);
        Assert.Equal("Alright, I will go chop down trees and gather logs for you.", response.Reply);
        Assert.False(response.NeedsClarification);
        Assert.False(response.KeepCurrentTask);
        Assert.Equal("Gather 10 oak logs and deposit into chest", response.TaskSummary);
        Assert.Equal("function main()\n  npc.say('Starting')\nend", response.Script);
    }

    [Fact]
    public void TryParse_WithMarkdownCodeFences_StripsFencesAndParses()
    {
        string json = "```json\n" +
            "{\n" +
            "  \"reply\": \"Following your lead.\",\n" +
            "  \"needs_clarification\": false,\n" +
            "  \"keep_current_task\": false,\n" +
            "  \"task_summary\": \"Follow player\",\n" +
            "  \"script\": \"function main() end\"\n" +
            "}\n" +
            "```";

        bool ok = LlmResponseParser.TryParse(json, out var response, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(response);
        Assert.Equal("Following your lead.", response.Reply);
        Assert.Equal("function main() end", response.Script);
    }

    [Fact]
    public void TryParse_ChitChatResponse_ParsesCorrectly()
    {
        string json = @"
        {
            ""reply"": ""Good morning! Nice weather today."",
            ""needs_clarification"": false,
            ""keep_current_task"": true,
            ""task_summary"": """",
            ""script"": """"
        }";

        bool ok = LlmResponseParser.TryParse(json, out var response, out string? error);

        Assert.True(ok);
        Assert.NotNull(response);
        Assert.True(response.KeepCurrentTask);
        Assert.False(response.NeedsClarification);
        Assert.Equal(string.Empty, response.Script);
    }

    [Fact]
    public void TryParse_ClarificationResponse_ParsesCorrectly()
    {
        string json = @"
        {
            ""reply"": ""Which chest do you want me to put the items in?"",
            ""needs_clarification"": true,
            ""keep_current_task"": false,
            ""task_summary"": """",
            ""script"": """"
        }";

        bool ok = LlmResponseParser.TryParse(json, out var response, out string? error);

        Assert.True(ok);
        Assert.NotNull(response);
        Assert.True(response.NeedsClarification);
        Assert.False(response.KeepCurrentTask);
        Assert.Equal(string.Empty, response.Script);
    }

    [Fact]
    public void TryParse_LongReply_TruncatesTo200Chars()
    {
        string longReply = new string('A', 250);
        string json = $@"
        {{
            ""reply"": ""{longReply}"",
            ""needs_clarification"": false,
            ""keep_current_task"": false,
            ""task_summary"": ""Task"",
            ""script"": ""function main() end""
        }}";

        bool ok = LlmResponseParser.TryParse(json, out var response, out string? error);

        Assert.True(ok);
        Assert.NotNull(response);
        Assert.Equal(200, response.Reply.Length);
        Assert.Equal(new string('A', 200), response.Reply);
    }

    [Theory]
    [InlineData(@"{ ""needs_clarification"": false, ""keep_current_task"": false, ""task_summary"": ""t"", ""script"": """" }", "reply")]
    [InlineData(@"{ ""reply"": ""hi"", ""keep_current_task"": false, ""task_summary"": ""t"", ""script"": """" }", "needs_clarification")]
    [InlineData(@"{ ""reply"": ""hi"", ""needs_clarification"": false, ""task_summary"": ""t"", ""script"": """" }", "keep_current_task")]
    [InlineData(@"{ ""reply"": ""hi"", ""needs_clarification"": false, ""keep_current_task"": false, ""script"": """" }", "task_summary")]
    [InlineData(@"{ ""reply"": ""hi"", ""needs_clarification"": false, ""keep_current_task"": false, ""task_summary"": ""t"" }", "script")]
    public void TryParse_MissingRequiredProperty_Fails(string json, string missingProperty)
    {
        bool ok = LlmResponseParser.TryParse(json, out var response, out string? error);

        Assert.False(ok);
        Assert.Null(response);
        Assert.NotNull(error);
        Assert.Contains(missingProperty, error);
    }

    [Fact]
    public void TryParse_MalformedJson_FailsGracefully()
    {
        string malformed = "{ reply: not valid json }";

        bool ok = LlmResponseParser.TryParse(malformed, out var response, out string? error);

        Assert.False(ok);
        Assert.Null(response);
        Assert.NotNull(error);
        Assert.Contains("JSON parsing error", error);
    }

    [Fact]
    public void TryParse_EmptyOrWhitespace_FailsGracefully()
    {
        Assert.False(LlmResponseParser.TryParse(null, out _, out string? err1));
        Assert.NotNull(err1);

        Assert.False(LlmResponseParser.TryParse("   ", out _, out string? err2));
        Assert.NotNull(err2);
    }
}
