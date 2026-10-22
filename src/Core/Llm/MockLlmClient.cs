namespace VoxelKing.Core.Llm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Brain;

/// <summary>
/// Mock LLM client mapping player instruction keywords to example Lua brains (LLM_INTEGRATION §6).
/// Used offline, in all automated unit tests, and as an automatic fallback when no Gemini API key is configured.
/// </summary>
public sealed class MockLlmClient : ILlmClient
{
    private readonly Dictionary<string, BrainExample> _examplesByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional custom handler override for testing arbitrary responses.
    /// </summary>
    public Func<LlmRequest, LlmResponse?>? CustomHandler { get; set; }

    /// <summary>
    /// Optional simulated error to return instead of a successful response.
    /// </summary>
    public LlmError? SimulatedError { get; set; }

    /// <summary>
    /// Optional simulated artificial latency before returning responses.
    /// </summary>
    public TimeSpan SimulatedLatency { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Number of requests processed by this mock client.
    /// </summary>
    public int RequestCount { get; private set; }

    public MockLlmClient(IReadOnlyList<BrainExample>? examples = null)
    {
        if (examples != null && examples.Count > 0)
        {
            foreach (var ex in examples)
            {
                _examplesByName[ex.Name] = ex;
            }
        }
        else
        {
            LoadDefaultExamples();
        }
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        RequestCount++;

        if (SimulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(SimulatedLatency, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (SimulatedError != null)
        {
            return LlmResponse.Failure(SimulatedError);
        }

        if (CustomHandler != null)
        {
            var customResponse = CustomHandler(request);
            if (customResponse != null)
            {
                return customResponse;
            }
        }

        // Extract last user instruction
        string lastUserText = string.Empty;
        for (int i = request.Messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(request.Messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                lastUserText = request.Messages[i].Text;
                break;
            }
        }

        var brainResponse = GenerateBrainResponse(lastUserText);
        string rawJson = JsonSerializer.Serialize(brainResponse, new JsonSerializerOptions { WriteIndented = true });

        int promptTokens = PromptBuilder.EstimateTokens(request.SystemInstruction) + PromptBuilder.EstimateTokens(lastUserText);
        int outputTokens = PromptBuilder.EstimateTokens(rawJson);
        var usage = new LlmUsage(promptTokens, outputTokens, promptTokens + outputTokens);

        return LlmResponse.Success(brainResponse, rawJson, usage);
    }

    private LlmBrainResponse GenerateBrainResponse(string text)
    {
        string lower = text.ToLowerInvariant();

        // 1. Deliberately broken scripts for validator testing (F-307)
        if (lower.Contains("syntax_error") || lower.Contains("test_syntax"))
        {
            return CreateExampleResponse("syntax_error", "I'll try that syntax test.");
        }
        if (lower.Contains("unknown_api") || lower.Contains("test_unknown"))
        {
            return CreateExampleResponse("unknown_api", "I'll try that unknown API test.");
        }
        if (lower.Contains("busy_loop") || lower.Contains("test_busy"))
        {
            return CreateExampleResponse("busy_loop", "I'll try that busy loop test.");
        }
        if (lower.Contains("blocking_handler") || lower.Contains("test_blocking") || lower.Contains("blocking_in_handler"))
        {
            return CreateExampleResponse("blocking_in_handler", "I'll try that blocking handler test.");
        }

        // 2. Production example brains
        if (Regex.IsMatch(lower, @"\b(wood|logs?|trees?|chop)\b"))
        {
            return CreateExampleResponse("gather_logs", "Alright, I'll go chop down trees and gather logs for you.");
        }

        if (Regex.IsMatch(lower, @"\b(follow|come|stay close)\b"))
        {
            return CreateExampleResponse("follow_player", "Understood! I'll follow you wherever you go.");
        }

        if (Regex.IsMatch(lower, @"\b(walls?|build|stone wall)\b"))
        {
            return CreateExampleResponse("build_wall", "I'll construct a stone wall for you right now.");
        }

        if (Regex.IsMatch(lower, @"\b(guard|defend|protect|patrol)\b"))
        {
            return CreateExampleResponse("guard_area", "I will stand guard and keep this area safe.");
        }

        if (Regex.IsMatch(lower, @"\b(farm|wheat|crops?|harvest)\b"))
        {
            return CreateExampleResponse("farm_loop", "I'll tend to the crops and farm the wheat.");
        }

        if (Regex.IsMatch(lower, @"\b(night|sleep|bed|go home|dusk)\b"))
        {
            return CreateExampleResponse("go_home_at_night", "I will return home once dusk falls.");
        }

        if (Regex.IsMatch(lower, @"\b(deliver|chest|deposit|bring)\b"))
        {
            return CreateExampleResponse("deliver_items", "I'll deliver the items to your chest.");
        }

        // 3. Chit-chat / greetings
        if (Regex.IsMatch(lower, @"\b(hello|hi|hey|how are you|who are you|greetings|good morning|morning|good evening|good afternoon|friend)\b"))
        {
            return new LlmBrainResponse
            {
                Reply = "Hello there! Just going about my daily routine. How can I help you?",
                NeedsClarification = false,
                KeepCurrentTask = true,
                TaskSummary = string.Empty,
                Script = string.Empty
            };
        }

        // 4. Ambiguous / clarification needed
        if (Regex.IsMatch(lower, @"\b(help|what|asdf|thing|over there)\b") || string.IsNullOrWhiteSpace(text))
        {
            return new LlmBrainResponse
            {
                Reply = "I'm not quite sure what you mean. Could you clarify your instruction?",
                NeedsClarification = true,
                KeepCurrentTask = false,
                TaskSummary = string.Empty,
                Script = string.Empty
            };
        }

        // Default: If no keyword matched, ask for clarification
        return new LlmBrainResponse
        {
            Reply = "I didn't quite catch that. Could you tell me more specifically what you'd like me to do?",
            NeedsClarification = true,
            KeepCurrentTask = false,
            TaskSummary = string.Empty,
            Script = string.Empty
        };
    }

    private LlmBrainResponse CreateExampleResponse(string exampleName, string defaultReply)
    {
        if (_examplesByName.TryGetValue(exampleName, out var example))
        {
            return new LlmBrainResponse
            {
                Reply = !string.IsNullOrWhiteSpace(example.Reply) ? example.Reply : defaultReply,
                NeedsClarification = false,
                KeepCurrentTask = false,
                TaskSummary = !string.IsNullOrWhiteSpace(example.Instruction) ? example.Instruction : exampleName,
                Script = example.Script
            };
        }

        // Fallback minimal script if not loaded
        return new LlmBrainResponse
        {
            Reply = defaultReply,
            NeedsClarification = false,
            KeepCurrentTask = false,
            TaskSummary = exampleName,
            Script = $"function main()\n    npc.say(\"{defaultReply}\")\n    npc.wait(1)\nend\n"
        };
    }

    private void LoadDefaultExamples()
    {
        try
        {
            string? dir = BrainExampleLibrary.FindExamplesDirectory();
            if (dir != null && Directory.Exists(dir))
            {
                var loaded = BrainExampleLibrary.LoadFromDirectory(dir);
                foreach (var ex in loaded)
                {
                    _examplesByName[ex.Name] = ex;
                }
            }
        }
        catch
        {
            // Directory discovery is optional; fallback minimal examples will be used if needed
        }
    }
}
