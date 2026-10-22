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
        Assert.Contains("identical to Minecraft", sys);
        Assert.Contains("2 blocks tall and 1 block wide", sys);

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
        Assert.Contains("Dimensions & Coordinates", sys);

        // 3. API Reference & Runtime Limits
        Assert.Contains("# Lua Brain API Reference", sys);
        Assert.Contains("npc.go_to", sys);
        Assert.Contains("world.find_block", sys);
        Assert.Contains("memory.get", sys);
        Assert.Contains("### Runtime Limits", sys);
        Assert.Contains("2,000", sys);
        Assert.Contains("8,192 bytes", sys);

        // 4. Few-shot example (relative coordinates, mining, placing)
        Assert.Contains("# Example", sys);
        Assert.Contains("Player request:", sys);
        Assert.Contains("npc.pos():offset", sys);
        Assert.Contains("npc.mine", sys);
        Assert.Contains("npc.place", sys);

        // 5. Output Format & Schema
        Assert.Contains("# Output Format", sys);
        Assert.Contains("\"script\":", sys);
    }

    [Fact]
    public void SampleNpcSnapshot_MatchesExpectedFormat()
    {
        var builder = new PromptBuilder();

        var context = new NpcPromptContext
        {
            Name = "Bob",
            Position = new Vec3(10f, 64f, -12f),
            PlayerHeldItem = "oak_log x1",
            Inventory = new Dictionary<string, int>
            {
                { "oak_log", 3 },
                { "stone_pickaxe", 1 }
            },
            Instruction = "Dig straight down"
        };

        string userContent = builder.BuildUserContent(context);

        string expected =
            "### NPC State\n" +
            "| Name | Position | Inventory |\n" +
            "|---|---|---|\n" +
            "| Bob | (10, 64, -12) | oak_log x3, stone_pickaxe x1 |\n\n" +
            "Player held item: oak_log x1\n\n" +
            "Player request: \"Dig straight down\"";

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

        var prompt = builder.BuildPrompt(
            npc: npc,
            instruction: "Deposit your logs and take a rest",
            playerHeldItem: "cobblestone x5"
        );

        Assert.NotNull(prompt);
        Assert.Contains("### NPC State", prompt.UserContent);
        Assert.Contains("| Lumberjack | (10, 64, 20) | apple x2, cobblestone x50, oak_log x8 |", prompt.UserContent);
        Assert.Contains("Player held item: cobblestone x5", prompt.UserContent);
        Assert.Contains("Player request: \"Deposit your logs and take a rest\"", prompt.UserContent);

        Assert.True(prompt.SystemTokens > 0);
        Assert.True(prompt.UserTokens > 0);
        Assert.Equal(prompt.SystemTokens + prompt.UserTokens, prompt.TotalEstimatedTokens);
    }

    [Fact]
    public void UserContent_IsLeanAndCompact()
    {
        var builder = new PromptBuilder();

        var context = new NpcPromptContext
        {
            Name = "Alice",
            Position = new Vec3(5f, 64f, 5f),
            PlayerHeldItem = "iron_sword x1",
            Inventory = new Dictionary<string, int> { { "bread", 3 } },
            Instruction = "Defend the gate"
        };

        string userContent = builder.BuildUserContent(context);
        int tokens = PromptBuilder.EstimateTokens(userContent);

        // Lean format must remain well under 60 tokens
        Assert.True(tokens < 60, $"Expected lean tokens < 60, got {tokens}");
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
            Position = Vec3.Zero,
            Inventory = new Dictionary<string, int>(),
            Instruction = "Wait here"
        };

        string content = builder.BuildUserContent(context);

        Assert.Contains("### NPC State", content);
        Assert.Contains("| EmptyNpc | (0, 0, 0) | empty |", content);
        Assert.Contains("Player held item: empty", content);
        Assert.Contains("Player request: \"Wait here\"", content);
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
        var builder = new PromptBuilder();
        string sys = builder.BuildSystemInstruction();

        int searchIndex = 0;
        int jsonCount = 0;
        while ((searchIndex = sys.IndexOf("```json", searchIndex, StringComparison.Ordinal)) >= 0)
        {
            int start = searchIndex + 7;
            int end = sys.IndexOf("```", start, StringComparison.Ordinal);
            if (end < 0) break;

            string jsonSnippet = sys.Substring(start, end - start).Trim();
            searchIndex = end + 3;

            if (jsonSnippet.Contains("\"script\":") && !jsonSnippet.Contains("\"type\": \"object\""))
            {
                using var doc = JsonDocument.Parse(jsonSnippet);
                var root = doc.RootElement;
                Assert.True(root.TryGetProperty("script", out _));
                jsonCount++;
            }
        }

        Assert.True(jsonCount >= 1, $"Expected at least 1 valid JSON response example, found {jsonCount}.");
    }
}
