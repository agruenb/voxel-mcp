# Agent Rules — voxel-king

You are implementing a Godot 4.7 (.NET / C#) voxel game whose NPCs run LLM-generated Lua brains.

## Read first
1. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — structure, boundaries, threading (source of truth)
2. [docs/FEATURES.md](docs/FEATURES.md) — pick a feature whose dependencies are `Done`
3. [docs/LUA_BRAIN_API.md](docs/LUA_BRAIN_API.md) / [docs/LLM_INTEGRATION.md](docs/LLM_INTEGRATION.md) when touching brains or the LLM

## Workflow
- Work on **one feature** per session
- If the design must change, update the relevant doc **in the same change** and explain why.
  Do not silently deviate from the architecture.
- note discovered gaps as new `Todo` rows instead.

## Code rules
- `src/Core` must never reference Godot (`using Godot;` is forbidden there). Put all logic that can be tested without the engine in Core.
- `src/Game` contains Godot nodes only: rendering, input, UI, glue. No simulation rules in nodes.
- Only the main thread mutates world/NPC state. Workers get copies; results return via `MainThreadDispatcher`.
- No static mutable singletons in Core; inject dependencies through constructors.
- Nullable reference types on; Core builds with warnings as errors.
- Every Core feature ships with xUnit tests in `tests/VoxelKing.Core.Tests`. `dotnet test` must stay green.
- Lua API changes: edit the `[LuaApi]` binding **and** `docs/LUA_BRAIN_API.md`, bump `BRAIN_API_VERSION`, update examples.
- Never commit API keys. Secrets live in `user://config.json` or `GEMINI_API_KEY`.
- Preserve existing comments/docstrings unrelated to your change.

## Verification before marking Done
- `dotnet build` and `dotnet test` succeed from the repo root.
- The Godot project builds and the relevant scene runs without errors in the output panel.
- Acceptance criteria of the feature are demonstrably met (describe how in the PR/commit).
