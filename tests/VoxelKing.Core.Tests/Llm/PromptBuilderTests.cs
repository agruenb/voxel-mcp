namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using Xunit;

public class PromptBuilderTests
{
    [Fact]
    public void SystemInstruction_IsIdenticalBetweenCalls_CacheFriendly()
    {
        var builder = new PromptBuilder();

        string sys1 = builder.BuildSystemInstruction();
        string sys2 = builder.BuildSystemInstruction();

        Assert.NotNull(sys1);
        Assert.NotEmpty(sys1);
        Assert.Same(sys1, sys2);
    }

    [Fact]
    public void BuiltPrompt_DifferentNpcsShareIdenticalSystemInstruction()
    {
        var builder = new PromptBuilder();

        var npc1 = new NpcState(new EntityId(1), "Bob", Vec3.Zero, personality: "Grumpy farmer");
        var npc2 = new NpcState(new EntityId(2), "Alice", Vec3.Zero, personality: "Cheerful guard");

        var prompt1 = builder.BuildPrompt(npc1, "Harvest the wheat");
        var prompt2 = builder.BuildPrompt(npc2, "Patrol the north wall");

        Assert.Equal(prompt1.SystemInstruction, prompt2.SystemInstruction);
        Assert.NotEqual(prompt1.UserContent, prompt2.UserContent);
    }

    [Fact]
    public void SystemInstruction_ContainsAllRequiredSections()
    {
        var builder = new PromptBuilder();
        string sys = builder.BuildSystemInstruction();

        // 1. Role & Purpose
        Assert.Contains("# Role & Purpose", sys);
        Assert.Contains("Lua 5.2", sys);

        // 2. Hard Rules
        Assert.Contains("# Hard Rules", sys);
        Assert.Contains("Define main()", sys);
        Assert.Contains("API only", sys);
        Assert.Contains("No top-level work", sys);
        Assert.Contains("Blocking calls only in main", sys);
        Assert.Contains("Always check return values", sys);
        Assert.Contains("unreachable", sys);
        Assert.Contains("Avoid busy loops", sys);
        Assert.Contains("Restartability", sys);
        Assert.Contains("In-character reply", sys);
        Assert.Contains("Security", sys);

        // 3. API Reference & Runtime Limits
        Assert.Contains("# Lua Brain API Reference", sys);
        Assert.Contains("npc.go_to", sys);
        Assert.Contains("world.find_block", sys);
        Assert.Contains("memory.get", sys);
        Assert.Contains("### Runtime Limits", sys);
        Assert.Contains("2,000", sys);
        Assert.Contains("8,192 bytes", sys);

        // 4. Few-shot examples
        Assert.Contains("# Few-Shot Examples", sys);
        Assert.Contains("### Example: Chit-chat", sys);
        Assert.Contains("### Example: Clarification Needed", sys);

        // 5. Output Format & Schema
        Assert.Contains("# Output Format", sys);
        Assert.Contains("\"reply\":", sys);
        Assert.Contains("\"needs_clarification\":", sys);
        Assert.Contains("\"keep_current_task\":", sys);
        Assert.Contains("\"task_summary\":", sys);
        Assert.Contains("\"script\":", sys);
    }

    [Fact]
    public void SampleNpcSnapshot_MatchesExpectedFormat()
    {
        var builder = new PromptBuilder();
        double currentTime = 600.0; // 10 minutes into simulation

        var events = new List<NpcMemoryEvent>
        {
            new SpeechHeardEvent("Player Adrian", "thanks!", isPlayer: true, timestamp: 300.0),
            new BrainErrorEvent("attempt to index nil value", timestamp: 300.0)
        };

        var context = new NpcPromptContext
        {
            Name = "Bob",
            Personality = "grumpy but helpful farmer",
            Skills = new[] { "farming", "mining" },
            CurrentTask = "Gathering logs for the player",
            TaskRunningSeconds = 180.0, // 3 minutes
            KnownPlaces = new[] { "home", "player_chest", "wheat_field" },
            Inventory = new Dictionary<string, int>
            {
                { "oak_log", 3 },
                { "stone_pickaxe", 1 }
            },
            RecentEvents = events,
            Instructor = "Player \"Adrian\"",
            Conversation = new[]
            {
                new ConversationTurn("Player", "Hey Bob"),
                new ConversationTurn("Bob", "Need something?")
            },
            Instruction = "Stop that and build a small stone wall around the wheat field",
            CurrentTime = currentTime
        };

        string userContent = builder.BuildUserContent(context);

        string expected =
            "NPC: name=Bob, personality=\"grumpy but helpful farmer\", skills=[farming, mining]\n" +
            "Current task: \"Gathering logs for the player\" (running 3 min)\n" +
            "Known places: home, player_chest, wheat_field\n" +
            "Inventory: oak_log x3, stone_pickaxe x1\n" +
            "Recent events (newest last, max 2):\n" +
            "- Player Adrian said: \"thanks!\" (5 min ago)\n" +
            "- Task failed: brain_error \"attempt to index nil value\" (5 min ago)\n" +
            "Instructor: Player \"Adrian\"\n" +
            "Conversation (last 2 turns):\n" +
            "Player: \"Hey Bob\"\n" +
            "Bob: \"Need something?\"\n" +
            "Instruction: \"Stop that and build a small stone wall around the wheat field\"";

        Assert.Equal(expected, userContent);
    }

    [Fact]
    public void BuildPrompt_FromNpcState_CorrectlyExtractsAllFields()
    {
        var builder = new PromptBuilder();
        var npc = new NpcState(
            id: new EntityId(10),
            name: "Lumberjack",
            position: new Vec3(10f, 64f, 20f),
            personality: "eager woodsman",
            skills: new[] { "forestry", "woodworking" },
            home: new Vec3(0f, 64f, 0f)
        );

        npc.Inventory.AddItem("oak_log", 8, out _);
        npc.Inventory.AddItem("apple", 2, out _);
        npc.Memory.RememberPlace("forest_camp", new Vec3i(30, 64, 40));
        npc.CurrentTaskSummary = "Chopping trees";
        npc.TaskStartTime = 100.0;

        npc.Memory.RecordSpeechHeard("Adrian", "watch out for wolves", isPlayer: true, timestamp: 120.0);
        npc.Memory.RecordInstruction("Adrian", "bring logs to camp", timestamp: 140.0);

        var prompt = builder.BuildPrompt(
            npc: npc,
            instruction: "Deposit your logs and take a rest",
            instructor: "Player \"Adrian\"",
            currentTime: 220.0
        );

        Assert.NotNull(prompt);
        Assert.Contains("name=Lumberjack", prompt.UserContent);
        Assert.Contains("personality=\"eager woodsman\"", prompt.UserContent);
        Assert.Contains("skills=[forestry, woodworking]", prompt.UserContent);
        Assert.Contains("Current task: \"Chopping trees\" (running 2 min)", prompt.UserContent);
        Assert.Contains("Known places: home, forest_camp", prompt.UserContent);
        Assert.Contains("Inventory: apple x2, oak_log x8", prompt.UserContent);
        Assert.Contains("Recent events (newest last, max 2):", prompt.UserContent);
        Assert.Contains("- Player Adrian said: \"watch out for wolves\" (1 min ago)", prompt.UserContent);
        Assert.Contains("Instructor: Player \"Adrian\"", prompt.UserContent);
        Assert.Contains("Instruction: \"Deposit your logs and take a rest\"", prompt.UserContent);

        Assert.True(prompt.SystemTokens > 0);
        Assert.True(prompt.UserTokens > 0);
        Assert.Equal(prompt.SystemTokens + prompt.UserTokens, prompt.TotalEstimatedTokens);
    }

    [Fact]
    public void BudgetTrimming_TrimsOldestConversationTurnsThenEvents()
    {
        var builder = new PromptBuilder();

        var events = new List<NpcMemoryEvent>();
        for (int i = 0; i < 15; i++)
        {
            events.Add(new SpeechSpokeEvent($"Spoke event #{i}", timestamp: i * 10));
        }

        var turns = new List<ConversationTurn>();
        for (int i = 0; i < 6; i++)
        {
            turns.Add(new ConversationTurn("Speaker", $"Turn message #{i} with extra padding words to consume tokens"));
        }

        var context = new NpcPromptContext
        {
            Name = "Alice",
            Personality = "Guard",
            RecentEvents = events,
            Conversation = turns,
            Instruction = "Defend the gate"
        };

        // Budget that fits basic info but forces trimming
        var tightOptions = new PromptBuilderOptions
        {
            MaxEvents = 15,
            MaxConversationTurns = 4,
            MaxUserContentTokens = 75
        };

        string userContent = builder.BuildUserContent(context, tightOptions);
        int tokens = PromptBuilder.EstimateTokens(userContent);

        Assert.True(tokens <= tightOptions.MaxUserContentTokens);
        // Ensure newest items survived, not oldest
        if (userContent.Contains("Spoke event #"))
        {
            Assert.Contains("Spoke event #14", userContent);
            Assert.DoesNotContain("Spoke event #0", userContent);
        }
    }

    [Fact]
    public void EstimateTokens_ReturnsExpectedValues()
    {
        Assert.Equal(0, PromptBuilder.EstimateTokens(null));
        Assert.Equal(0, PromptBuilder.EstimateTokens(""));
        Assert.Equal(1, PromptBuilder.EstimateTokens("a"));
        Assert.Equal(1, PromptBuilder.EstimateTokens("abcd"));
        Assert.Equal(2, PromptBuilder.EstimateTokens("abcde"));
        Assert.Equal(25, PromptBuilder.EstimateTokens(new string('x', 100)));
    }

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(-5, "0s")]
    [InlineData(15, "15s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1 min")]
    [InlineData(180, "3 min")]
    [InlineData(3599, "59 min")]
    [InlineData(3600, "1h")]
    [InlineData(4320, "1h 12min")]
    public void FormatDuration_FormatsExpectedStrings(double seconds, string expected)
    {
        Assert.Equal(expected, PromptBuilder.FormatDuration(seconds));
    }

    [Fact]
    public void UserContent_HandlesEmptyAndNullCasesGracefully()
    {
        var builder = new PromptBuilder();

        var context = new NpcPromptContext
        {
            Name = "EmptyNpc",
            Personality = "",
            Skills = Array.Empty<string>(),
            CurrentTask = null,
            KnownPlaces = Array.Empty<string>(),
            Inventory = new Dictionary<string, int>(),
            RecentEvents = Array.Empty<NpcMemoryEvent>(),
            Instructor = null,
            Conversation = Array.Empty<ConversationTurn>(),
            Instruction = "Wait here"
        };

        string content = builder.BuildUserContent(context);

        Assert.Contains("NPC: name=EmptyNpc, personality=\"\", skills=[]", content);
        Assert.Contains("Current task: none", content);
        Assert.Contains("Known places: none", content);
        Assert.Contains("Inventory: empty", content);
        Assert.Contains("Recent events: none", content);
        Assert.Contains("Instructor: none", content);
        Assert.DoesNotContain("Conversation", content);
        Assert.Contains("Instruction: \"Wait here\"", content);
    }

    [Fact]
    public void BuildRepairContent_FormatsErrorsProperly()
    {
        var builder = new PromptBuilder();

        var errors = new List<ValidationError>
        {
            new ValidationError(Stage: 3, Line: 15, Message: "syntax error near 'end'"),
            new ValidationError(Stage: 4, Line: null, Message: "global function 'main' is not defined")
        };

        string repair = builder.BuildRepairContent(errors);

        Assert.Contains("failed validation with the following error(s):", repair);
        Assert.Contains("- [Stage 3, Line 15] syntax error near 'end'", repair);
        Assert.Contains("- [Stage 4] global function 'main' is not defined", repair);
        Assert.Contains("repair the script", repair);
        Assert.Contains("Return a complete, valid JSON object", repair);
    }

    [Fact]
    public void FewShotExamples_AreValidJsonMatchingSchema()
    {
        // Tests that few shot examples generated in the prompt contain parseable JSON
        var builder = new PromptBuilder();
        string sys = builder.BuildSystemInstruction();

        // Find all json code blocks in the system instruction
        int searchIndex = 0;
        int jsonCount = 0;
        while ((searchIndex = sys.IndexOf("```json", searchIndex, StringComparison.Ordinal)) >= 0)
        {
            int start = searchIndex + 7;
            int end = sys.IndexOf("```", start, StringComparison.Ordinal);
            if (end < 0) break;

            string jsonSnippet = sys.Substring(start, end - start).Trim();
            searchIndex = end + 3;

            // Only parse if it looks like a response object (not the schema definition itself)
            if (jsonSnippet.Contains("\"reply\":") && jsonSnippet.Contains("\"script\":") && !jsonSnippet.Contains("\"type\": \"object\""))
            {
                using var doc = JsonDocument.Parse(jsonSnippet);
                var root = doc.RootElement;
                Assert.True(root.TryGetProperty("reply", out _));
                Assert.True(root.TryGetProperty("needs_clarification", out _));
                Assert.True(root.TryGetProperty("keep_current_task", out _));
                Assert.True(root.TryGetProperty("task_summary", out _));
                Assert.True(root.TryGetProperty("script", out _));
                jsonCount++;
            }
        }

        Assert.True(jsonCount >= 3, $"Expected at least 3 valid JSON response examples, found {jsonCount}.");
    }
}
