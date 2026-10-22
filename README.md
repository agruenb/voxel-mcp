# Voxel King

A voxel sandbox game built with **Godot 4.7 (.NET / C#)** where NPCs are guided by natural language instructions, powered by LLM-compiled sandboxed Lua behaviour scripts.

## Prerequisites & Tool Locations (macOS)

- **Godot Mono (4.7+):**
  - Application bundle: `/Applications/Godot_mono.app`
  - Executable: `/Applications/Godot_mono.app/Contents/MacOS/Godot`
- **.NET SDK (8.0+):**
  - Path: `/usr/local/share/dotnet/dotnet`
  - Ensure dotnet is in your `PATH`:
    ```bash
    export PATH="$PATH:/usr/local/share/dotnet"
    ```

## Development & Build

### Running Tests
Execute xUnit tests for the engine-independent `VoxelKing.Core` library:
```bash
dotnet test
```

### Building the Solution
```bash
dotnet build
```

### Running the Game
Run the game using Godot:
```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot --path .
```
Or run headless for verification:
```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit
```

## Configuration System

Configuration is managed hierarchically across three sources:
1. `config/game_config.json` — Committed base defaults (world parameters, NPC simulation rates, Lua brain sandbox limits, LLM settings, and debug flags).
2. `user://config.json` — Local user overrides (e.g. Gemini API key and dev preferences, uncommitted).
3. `GEMINI_API_KEY` — Environment variable overriding any API key configured in files.

In Core, configuration is injected into services via `IGameConfigProvider` / `IGameConfig` (no static mutable globals).

## Documentation

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — System boundaries, threading model, and architectural rules.
- [FEATURES.md](docs/FEATURES.md) — Implementation roadmap and feature status backlog.
- [COMMANDS.md](docs/COMMANDS.md) — In-game debug console commands, controls, and shortcut hotkeys.
- [LUA_BRAIN_API.md](docs/LUA_BRAIN_API.md) — Sandboxed Lua API surface for NPC brains.
- [LLM_INTEGRATION.md](docs/LLM_INTEGRATION.md) — Gemini prompt pipeline and Lua compiler.
