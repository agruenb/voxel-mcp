# LLM Integration — Gemini Brain Compiler

> Scope: how a player instruction becomes a validated Lua brain. Runtime execution of the brain is
> described in [LUA_BRAIN_API.md](LUA_BRAIN_API.md).

## 1. Principles

- **One request per instruction** (+ max 2 repair retries). No tool/function calling, no MCP.
- **The model never receives world data.** It receives: API reference, rules, few-shot examples, the
  NPC profile, a short memory summary, known place *names*, and the instruction. The script discovers
  the world at runtime through `world.*` queries.
- **Structured output:** the model must answer with JSON matching a fixed schema
  (`responseMimeType: application/json` + `responseSchema`).

## 2. Request

Endpoint: `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`
Header: `x-goog-api-key: <key>`. Model name and generation parameters come from config
(`llm.model`, `llm.temperature` ≈ 0.3, `llm.maxOutputTokens`, `llm.timeoutSeconds`).

### 2.1 System instruction (built by `PromptBuilder`, mostly static → cache-friendly)

1. Role: "You generate executable Lua 5.2 behaviour scripts for an NPC in a voxel game. Players and NPCs are 2 blocks tall and 1 block wide (identical to Minecraft). Blocks are 1x1x1 cubes. Your sole job is to write a high-quality Lua script that performs the player's request."
2. Hard rules:
   1. Define `main()`: Every script MUST define a global `function main()`.
   2. API only: Only use listed functions from the API reference.
   3. No top-level work: Top level is exclusively for constants, helpers, and event handlers.
   4. Blocking calls only in `main()`: `npc.go_to`, `npc.mine`, `npc.place`, etc.
   5. Always check return values (`ok, reason`).
   6. Avoid busy loops: Yield or call `npc.wait(seconds)`.
   7. Restartability: Store persistent state in `memory`.
   8. Dimensions & Coordinates: NPCs and players occupy 2 vertical blocks (feet at `pos`, head at `pos.y + 1`).
3. **API reference** — generated from `LuaApiRegistry` (signatures, descriptions, failure reasons, limits).
4. One canonical few-shot example demonstrating relative coordinates (`pos:offset(...)`), block mining, and block placing.
5. Output format description (mirrors the schema).

### 2.2 User content (dynamic, lean)

```text
### NPC State
| Name | Position | Inventory |
|---|---|---|
| Charlie | (10, 64, -12) | cobblestone x50 |

Player held item: oak_log x1

Player request: "Dig straight down"
```

## 3. Response schema

```json
{
  "type": "object",
  "properties": {
    "script":              { "type": "string", "description": "Complete executable Lua 5.2 behaviour script defining function main()" }
  },
  "required": ["script"]
}
```

Optional properties (`reply`, `needs_clarification`, `keep_current_task`, `task_summary`) are accepted with default values if returned by external providers.

Handling:

| Case | Effect |
|---|---|
| Valid `script` | `BrainHost.Swap(script)`; `task_summary` (defaulting to the player's request) stored in memory |
| `needs_clarification` or `keep_current_task` | NPC says `reply` if present; current brain continues |
| Invalid after retries | NPC says a fallback line ("Sorry, I didn't quite get that."); current brain continues; failure logged |
| Network/timeout/quota error | NPC says a fallback line ("Hmm… give me a moment, I can't think right now."); logged |

## 4. Validation pipeline (`BrainValidator`)

1. **Parse** JSON, enforce schema and length limits.
2. **Size** ≤ 8 KB.
3. **Compile** in a fresh sandbox; capture syntax errors with line numbers.
4. **Top-level execution** with 10 000-instruction budget; must define global function `main`; only
   `main`, `on_*` handlers and locals may be defined at top level.
5. **Dry run** against `MockWorld`: run `main` for up to 200 resumes / 200 000 instructions. Mock
   actions succeed, queries return plausible deterministic values (including `nil` cases). Each
   handler is invoked once with sample arguments. Catches unknown globals, wrong arity/types, calling
   blocking functions in handlers, busy loops.
6. Result: `ValidationResult { ok, errors[] { stage, line?, message } }`.

**Repair loop:** on failure, `BrainCompiler` sends a follow-up turn (same conversation) containing the
error list and asks for a corrected full JSON response. Max retries configurable (default 2).

## 5. Request management (`LlmRequestQueue`)

- Global concurrency limit (default 2) and simple token-bucket rate limit (requests/minute).
- One in-flight request per NPC; a new instruction to the same NPC cancels the older one.
- Timeout per request (default 30 s); retry with exponential backoff on HTTP 429/500/503 (max 2).
- Usage logging (prompt/output tokens from `usageMetadata`) → aggregated in the debug overlay.
- Full request/response logs written to `user://logs/llm/<timestamp>_<npc>.json`.

## 6. Mock client

`MockLlmClient` maps keywords in the instruction to example brains in `brains/examples/`
(e.g. "wood|log" → `gather_logs.lua`, "follow" → `follow_player.lua`). Used:
- in all automated unit/integration tests,
- to test the validator with deliberately broken scripts (`brains/examples/invalid/*.lua`).

## 7. Prompt quality loop (development process)

- Maintain `tests/prompts/instructions.txt`: a list of representative player instructions.
- A dev command (`dotnet run --project tools/PromptEval` — future feature, see FEATURES.md "Future") sends each through the
  real compiler and reports validation pass rate, retries used, and token usage. Run it whenever the
  API or system prompt changes.
