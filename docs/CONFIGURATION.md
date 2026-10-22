# Game Configuration Reference

Configuration files are located in `config/` and loaded in layers by `GameConfigLoader`:
1. `GameConfig.cs` (Hardcoded defaults in Core)
2. `config/game_config.json` (Project defaults)
3. `user://config.json` or `config/user_config.json` (User overrides / secrets)
4. `GEMINI_API_KEY` (Environment variable override)

---

## 1. `brainLimits` — Lua Sandbox & Memory Quotas

Controls execution limits and storage quotas for NPC Lua behaviour brains.

| Property | Type | Default | Description |
|---|---|---|---|
| `maxInstructionsPerTick` | int | `2000` | Instruction quota for `main()` execution during a single brain tick before forcing a yield to the next frame. Prevents NPC scripts from stalling the main thread. |
| `maxConsecutiveForcedYields` | int | `50` | Maximum consecutive ticks an NPC script may yield without executing an action or waiting. On violation, the brain is terminated with a `"busy loop"` runtime error. |
| `maxInstructionsPerHandler` | int | `5000` | Instruction budget for an individual event handler callback (e.g., `on_heard`). Aborts with `"handler budget exceeded"` if exceeded. |
| `maxTopLevelChunkInstructions` | int | `10000` | Instruction budget for compiling and executing top-level script code outside functions. |
| `maxScriptSizeBytes` | int | `8192` | Maximum allowable Lua script file size in bytes (8 KB). Scripts exceeding this are rejected at compile time. |
| `maxSayLength` | int | `200` | Maximum character length of a spoken message in `say()` speech bubbles. |
| `maxMemoryKeys` | int | `64` | Maximum number of distinct key-value entries permitted in an NPC's persistent KV store. |
| `maxMemoryTotalBytes` | int | `16384` | Total storage limit in bytes across all keys and values in an NPC's KV store (16 KB). Setting values that exceed this throws an exception. |
| `maxMemoryKeyLength` | int | `64` | Maximum character length of an individual key name in the KV store. |
| `maxMemoryStringValueLength` | int | `4096` | Maximum length in characters for a string value stored in the KV store. |
| `maxMemoryPlaces` | int | `64` | Maximum number of named voxel locations an NPC can remember (e.g. `"home"`, `"player_chest"`). |
| `maxMemoryPlaceNameLength` | int | `64` | Maximum character length of a named place identifier. |
| `memoryEventCapacity` | int | `50` | Maximum number of recent events retained in the FIFO event ring buffer. Oldest events are evicted when capacity is reached. Used by prompt generation. |

---

## 2. `world` — Generation & Perception

| Property | Type | Default | Description |
|---|---|---|---|
| `chunkSize` | int | `32` | Dimensions of a cubic voxel chunk ($32 \times 32 \times 32$ blocks). |
| `loadedChunkRadius` | int | `6` | Radius of chunks loaded and meshed around the player camera. |
| `seed` | int | `1337` | World generation pseudo-random noise seed. |
| `perceptionRadiusBlocks` | int | `24` | Maximum distance (in blocks) within which an NPC can perceive and search for voxel blocks (`world.find_block`, etc.). |
| `perceptionRadiusEntities` | int | `32` | Maximum distance (in meters) within which an NPC can perceive other entities (`world.find_entity`, etc.). |

---

## 3. `npc` — Simulation & LOD Ticking

| Property | Type | Default | Description |
|---|---|---|---|
| `physicsTickRateHz` | int | `60` | Physics simulation tick rate in Hz for kinematic movement and gravity. |
| `brainTickRateHz` | int | `10` | Normal brain decision tick frequency for NPCs within the LOD distance threshold. |
| `distantBrainTickRateHz` | int | `2` | Throttled brain tick frequency for NPCs far from the player. |
| `lodDistanceThreshold` | float | `64.0` | Distance in meters from the player beyond which NPCs transition to distant LOD brain scheduling. |
| `sayRadius` | float | `16.0` | Hearing radius in meters for speech broadcast (`EntitySpoke` events). |
| `sayRateLimitSeconds` | float | `2.0` | Minimum interval in seconds between consecutive `say()` actions by an NPC. |

---

## 4. `llm` — Gemini Compiler Integration

| Property | Type | Default | Description |
|---|---|---|---|
| `model` | string | `"gemini-2.5-flash"` | Gemini model used for compiling player dialogue instructions into Lua scripts. |
| `temperature` | float | `0.3` | Model generation sampling temperature (lower = more deterministic code generation). |
| `maxOutputTokens` | int | `2048` | Maximum output tokens in the JSON response from Gemini. |
| `timeoutSeconds` | int | `30` | Network timeout for Gemini API calls in seconds. |
| `maxRetries` | int | `2` | Maximum number of repair retries when generated Lua fails validation. |
| `maxConcurrentRequests` | int | `2` | Maximum parallel requests allowed in the LLM queue. |
| `requestsPerMinute` | int | `60` | Token-bucket rate limit ceiling in requests per minute (<= 0 disables throttling). |
| `apiKey` | string | `""` | Gemini API key. Never commit this key! Set via `user://config.json` or `GEMINI_API_KEY` environment variable. |

---

## 5. `player` — Movement & Controller

| Property | Type | Default | Description |
|---|---|---|---|
| `walkSpeed` | float | `5.0` | Horizontal walk speed in meters per second. |
| `sprintSpeed` | float | `8.5` | Horizontal sprint speed in meters per second. |
| `jumpVelocity` | float | `7.5` | Upward velocity impulse applied when jumping. |
| `gravity` | float | `20.0` | Downward gravitational acceleration in $\text{m/s}^2$. |
| `flySpeed` | float | `15.0` | Flight speed when creative fly mode is toggled. |
| `sprintFlyMultiplier` | float | `2.0` | Multiplier applied to flight speed when sprinting. |
| `mouseSensitivity` | float | `0.003` | First-person look sensitivity. |
| `maxStepHeight` | float | `1.1` | Maximum vertical obstacle height the player can step up automatically without jumping. |
| `eyeHeight` | float | `1.6` | Camera height offset from feet position in meters. |
| `bodyRadius` | float | `0.35` | Cylinder radius of the player collision shape in meters. |
| `bodyHeight` | float | `1.8` | Height of the player collision cylinder in meters. |
| `enableStepAssist` | bool | `true` | Enables smooth step-up climbing over 1-block steps. |
| `enableStepSmoothing` | bool | `true` | Smooths camera transitions when ascending steps. |

---

## 6. `debug` — Visualizers & Diagnostics

| Property | Type | Default | Description |
|---|---|---|---|
| `showOverlay` | bool | `false` | Displays real-time HUD with FPS, player position, chunk coordinates, and target voxel. |
| `drawPaths` | bool | `false` | Renders 3D waypoint lines for NPC pathfinding paths. |
| `logLlmRequests` | bool | `true` | Logs prompts and raw responses from LLM interactions for debugging. |
| `logLevel` | string | `"Info"` | Minimum console log level (`"Debug"`, `"Info"`, `"Warning"`, `"Error"`). |
| `llmLogDirectory` | string | `"user://logs/llm"` | Directory where full LLM JSON audit logs are stored. |
