namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;

/// <summary>
/// Authoritative prompt builder implementing LLM_INTEGRATION §2 (F-401).
/// Compiles the static, cacheable system instruction and dynamic per-turn user content.
/// </summary>
public sealed class PromptBuilder : IPromptBuilder
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly LuaApiRegistry _registry;
    private readonly PromptBuilderOptions _defaultOptions;
    private readonly string _systemInstruction;

    /// <summary>
    /// Initializes a new instance of <see cref="PromptBuilder"/>.
    /// </summary>
    /// <param name="registry">Authoritative Lua API registry (defaults to <see cref="LuaApiRegistry.Default"/>).</param>
    /// <param name="examples">Optional list of few-shot example brains. If null, attempts to discover and load from disk.</param>
    /// <param name="defaultOptions">Optional default budgeting and formatting options.</param>
    public PromptBuilder(
        LuaApiRegistry? registry = null,
        IReadOnlyList<BrainExample>? examples = null,
        PromptBuilderOptions? defaultOptions = null)
    {
        _registry = registry ?? LuaApiRegistry.Default;
        _defaultOptions = defaultOptions ?? new PromptBuilderOptions();

        var selectedExamples = SelectFewShotExamples(examples, _defaultOptions.MaxFewShotExamples);
        _systemInstruction = BuildSystemInstructionInternal(_registry, selectedExamples);
    }

    /// <inheritdoc/>
    public string BuildSystemInstruction() => _systemInstruction;

    /// <inheritdoc/>
    public string BuildUserContent(NpcPromptContext context, PromptBuilderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RenderUserContent(context);
    }

    /// <inheritdoc/>
    public string BuildUserContent(
        NpcState npc,
        string instruction,
        string? instructor = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        PromptBuilderOptions? options = null,
        string? playerHeldItem = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(instruction);

        var context = NpcPromptContext.FromNpc(npc, instruction, instructor, conversation, currentTime, playerHeldItem);
        return BuildUserContent(context, options);
    }

    /// <inheritdoc/>
    public BuiltPrompt BuildPrompt(NpcPromptContext context, PromptBuilderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        string userContent = BuildUserContent(context, options);
        int systemTokens = EstimateTokens(_systemInstruction);
        int userTokens = EstimateTokens(userContent);

        return new BuiltPrompt(_systemInstruction, userContent, systemTokens, userTokens);
    }

    /// <inheritdoc/>
    public BuiltPrompt BuildPrompt(
        NpcState npc,
        string instruction,
        string? instructor = null,
        IReadOnlyList<ConversationTurn>? conversation = null,
        double currentTime = -1,
        PromptBuilderOptions? options = null,
        string? playerHeldItem = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(instruction);

        var context = NpcPromptContext.FromNpc(npc, instruction, instructor, conversation, currentTime, playerHeldItem);
        return BuildPrompt(context, options);
    }

    /// <inheritdoc/>
    public string BuildRepairContent(IReadOnlyList<ValidationError> errors, string? previousScript = null)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var sb = new StringBuilder();
        sb.AppendLine("The previously generated Lua brain script failed validation with the following error(s):");
        foreach (var err in errors)
        {
            string lineInfo = err.Line.HasValue ? $", Line {err.Line.Value}" : string.Empty;
            sb.AppendLine($"- [Stage {err.Stage}{lineInfo}] {err.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("Please repair the script to fix all validation errors above.");
        sb.AppendLine("Return a complete, valid JSON object matching the required schema: { \"script\": \"...\" }.");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Estimates the token count of a given string using the standard rule of thumb (~4 characters per token).
    /// </summary>
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / 4.0);
    }

    /// <summary>
    /// Formats a time duration in seconds into a compact, human-readable string (e.g. "45s", "3 min", "1h 12min").
    /// </summary>
    public static string FormatDuration(double seconds)
    {
        if (seconds <= 0)
        {
            return "0s";
        }

        if (seconds < 60)
        {
            return $"{(int)Math.Round(seconds)}s";
        }

        int totalMinutes = (int)Math.Floor(seconds / 60.0);
        if (totalMinutes < 60)
        {
            return $"{totalMinutes} min";
        }

        int hours = totalMinutes / 60;
        int remainingMinutes = totalMinutes % 60;
        return remainingMinutes > 0 ? $"{hours}h {remainingMinutes}min" : $"{hours}h";
    }

    private static string RenderUserContent(NpcPromptContext context)
    {
        var sb = new StringBuilder();

        // 1. NPC State table
        sb.AppendLine("### NPC State");
        sb.AppendLine("| Name | Position | Inventory |");
        sb.AppendLine("|---|---|---|");

        string inventoryStr;
        if (context.Inventory != null)
        {
            var items = context.Inventory
                .Where(kv => kv.Value > 0)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key} x{kv.Value}")
                .ToList();

            inventoryStr = items.Count > 0 ? string.Join(", ", items) : "empty";
        }
        else
        {
            inventoryStr = "empty";
        }

        string posStr = $"({Math.Round(context.Position.X, 1):0.#}, {Math.Round(context.Position.Y, 1):0.#}, {Math.Round(context.Position.Z, 1):0.#})";
        sb.AppendLine($"| {context.Name} | {posStr} | {inventoryStr} |");
        sb.AppendLine();

        // 2. Player held item
        string playerHeld = !string.IsNullOrWhiteSpace(context.PlayerHeldItem) ? context.PlayerHeldItem : "empty";
        sb.AppendLine($"Player held item: {playerHeld}");
        sb.AppendLine();

        // 3. Instruction
        sb.Append($"Player request: \"{context.Instruction}\"");

        return sb.ToString();
    }

    private static string BuildSystemInstructionInternal(
        LuaApiRegistry registry,
        IReadOnlyList<BrainExample> examples)
    {
        var sb = new StringBuilder();

        // 1. Role & Purpose
        sb.AppendLine("# Role & Purpose");
        sb.AppendLine("You generate executable Lua 5.2 behaviour scripts for an NPC in a voxel game.");
        sb.AppendLine("Players and NPCs are 2 blocks tall and 1 block wide (identical to Minecraft). Blocks are 1x1x1 cubes.");
        sb.AppendLine("Your sole job is to write a high-quality Lua script that performs the player's request.");
        sb.AppendLine();

        // 2. Hard Rules
        sb.AppendLine("# Hard Rules");
        sb.AppendLine("1. Define main(): Every script MUST define a global `function main()`. Execution begins there.");
        sb.AppendLine("2. API only: Only use functions from the API reference below. Do not use unlisted functions, standard Lua I/O (`io.*`), OS (`os.*`), debug (`debug.*`), or coroutine primitives directly.");
        sb.AppendLine("3. No top-level work: Do not execute loops, actions, or blocking calls at the top level. Top level is exclusively for declaring local constants, helper functions, and optional event handlers (`on_*`).");
        sb.AppendLine("4. Blocking calls only in main: Blocking actions (`npc.go_to`, `npc.go_near`, `npc.follow`, `npc.mine`, `npc.place`, `npc.pick_up`, `npc.drop`, `npc.give`, `npc.deposit`, `npc.withdraw`, `npc.wait`) are ONLY allowed in `main()` or helper functions called by `main()`. NEVER call blocking functions from event handlers (`on_*`).");
        sb.AppendLine("5. Always check return values: Blocking actions return `ok, reason`. Always check `if not ok then ... end` and handle failure reasons gracefully (\"unreachable\", \"stuck\", \"too_far\", \"no_item\", \"inventory_full\", \"invalid_target\", \"target_gone\", \"occupied\", \"interrupted\", \"timeout\").");
        sb.AppendLine("6. Avoid busy loops: Any loop must perform a blocking action or call `npc.wait(seconds)`. Scripts that loop without yielding will be terminated as busy loops.");
        sb.AppendLine("7. Restartability: Store persistent progress in `memory` (`memory.set`, `memory.get`) so that `main()` resumes properly if restarted.");
        sb.AppendLine("8. Dimensions & Coordinates: NPCs and players occupy 2 vertical blocks (feet at `pos`, head at `pos.y + 1`).");
        sb.AppendLine();

        // 3. API Reference
        sb.AppendLine(registry.GenerateMarkdownReference());
        sb.AppendLine();

        // 3.1 Runtime Limits
        sb.AppendLine("### Runtime Limits");
        sb.AppendLine("- Instructions per brain tick (`main`): 2,000 (forced yield, continues next tick)");
        sb.AppendLine("- Consecutive forced yields without action: 50 (error 'busy loop')");
        sb.AppendLine("- Instructions per handler invocation: 5,000 (error 'handler budget exceeded')");
        sb.AppendLine("- Top-level chunk execution budget: 10,000 instructions");
        sb.AppendLine("- Script size limit: 8,192 bytes (8 KB)");
        sb.AppendLine();

        // 4. Example
        sb.AppendLine("# Example");
        sb.AppendLine();
        if (examples != null && examples.Count > 0)
        {
            var first = examples[0];
            sb.AppendLine($"Player request: \"{first.Instruction}\"");
            sb.AppendLine("Output:");
            sb.AppendLine("```json");
            var dto = new { script = first.Script };
            sb.AppendLine(JsonSerializer.Serialize(dto, IndentedJsonOptions));
            sb.AppendLine("```");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Player request: \"Clear the block in front of you and place cobblestone\"");
            sb.AppendLine("Output:");
            sb.AppendLine("```json");
            var exampleDto = new
            {
                script = "function main()\n" +
                         "    local target_pos = npc.pos():offset(1, 0, 0)\n" +
                         "    if world.get_block(target_pos) ~= \"air\" then\n" +
                         "        local ok, reason = npc.mine(target_pos)\n" +
                         "        if not ok then\n" +
                         "            log(\"Mine failed: \" .. tostring(reason))\n" +
                         "            return\n" +
                         "        end\n" +
                         "    end\n" +
                         "    local ok, reason = npc.place(target_pos, \"cobblestone\")\n" +
                         "    if not ok then\n" +
                         "        log(\"Place failed: \" .. tostring(reason))\n" +
                         "        return\n" +
                         "    end\n" +
                         "    npc.done(\"Replaced block\")\n" +
                         "end"
            };
            sb.AppendLine(JsonSerializer.Serialize(exampleDto, IndentedJsonOptions));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // 5. Output Format Description
        sb.AppendLine("# Output Format");
        sb.AppendLine("You MUST respond with a JSON object strictly matching this schema:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"script\": \"Complete executable Lua brain source defining main()\"");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString().TrimEnd();
    }

    private static IReadOnlyList<BrainExample> SelectFewShotExamples(
        IReadOnlyList<BrainExample>? providedExamples,
        int maxExamples)
    {
        if (providedExamples != null && providedExamples.Count > 0)
        {
            return providedExamples
                .Where(e => e.IsValid && !string.IsNullOrWhiteSpace(e.Instruction))
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .Take(maxExamples)
                .ToList();
        }

        return CreateFallbackExamples().Take(Math.Max(1, maxExamples)).ToList();
    }

    private static IReadOnlyList<BrainExample> CreateFallbackExamples()
    {
        return new List<BrainExample>
        {
            BrainExampleLibrary.Parse(
                "replace_block",
                "replace_block.lua",
                "-- instruction: Clear the block in front of you and place cobblestone\n" +
                "function main()\n" +
                "    local target_pos = npc.pos():offset(1, 0, 0)\n" +
                "    if world.get_block(target_pos) ~= \"air\" then\n" +
                "        local ok, reason = npc.mine(target_pos)\n" +
                "        if not ok then\n" +
                "            log(\"Mine failed: \" .. tostring(reason))\n" +
                "            return\n" +
                "        end\n" +
                "    end\n" +
                "    local ok, reason = npc.place(target_pos, \"cobblestone\")\n" +
                "    if not ok then\n" +
                "        log(\"Place failed: \" .. tostring(reason))\n" +
                "        return\n" +
                "    end\n" +
                "    npc.done(\"Replaced block\")\n" +
                "end")
        };
    }
}
