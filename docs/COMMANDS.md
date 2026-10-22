# Debug Commands & Controls Reference

This document catalogs all developer commands, keyboard shortcuts, and debug hotkeys available in **Voxel King**.

---

## 1. Keyboard Shortcuts & Controls

### Gameplay & Movement
| Key / Input | Action | Notes |
|---|---|---|
| **W, A, S, D** | Move | Standard first-person walking |
| **Space** | Jump | Jumps 1 block high |
| **Shift** (Hold) | Sprint | Increases movement speed |
| **F** | Toggle Fly Mode | Free flying mode (no gravity/collision) |
| **Esc** | Toggle Mouse Capture | Releases mouse cursor / closes dialogue or chests |
| **1 – 9** | Select Hotbar Slot | Select active item slot (0–8) |
| **Left Mouse Button (LMB)** | Break Block | Breaks aimed block according to its hardness time |
| **Right Mouse Button (RMB)** | Place Block | Places selected block against aimed block face |
| **E** | Interact | Opens container (chest) or talks to NPC |

---

### Debug NPC Shortcuts
| Key | Action | Description |
|---|---|---|
| **F4** | **Spawn NPC** | Spawns an NPC 3 meters in front of the player on the terrain |
| **F5** | **Move Nearest NPC** | Sets velocity on the nearest NPC (3.0 m/s) in player's forward direction |
| **F6** | **Jump Nearest NPC** | Commands the nearest NPC to perform a 1-block jump |
| **F7** | **Stop Nearest NPC** | Immediately stops the nearest NPC's horizontal movement |

---

## 2. In-Game Debug Console

Toggle the console at any time using:
- **`F1`** or **`` ` ``** (Grave / Tilde)

Opening the console pauses player camera movement and releases the mouse cursor. Press **`Esc`** or **`F1`** to close it.

### Available Console Commands

Commands can be entered with or without a leading slash `/`.

| Command | Arguments | Example | Description |
|---|---|---|---|
| `help` | — | `help` | Lists all available commands and keyboard shortcuts |
| `spawn_npc` (or `spawn`) | `[name]` | `spawn_npc Alice` | Spawns an NPC with the given name 3 meters in front of the player |
| `move_npc` (or `move`) | `<id \| name> <vx> <vz>` | `move_npc Bob 3 0` | Sets horizontal velocity (in m/s) on the specified NPC |
| `jump_npc` (or `jump`) | `<id \| name>` | `jump_npc Bob`<br>`jump_npc 1` | Triggers a 1-block vertical jump on the target NPC if on ground |
| `stop_npc` (or `stop`) | `<id \| name>` | `stop_npc Bob` | Stops all horizontal velocity on the target NPC |
| `list_npcs` (or `list`) | — | `list_npcs` | Displays all active NPCs with IDs, names, positions, LOD tiers, and ground/wall collision states |

---

## 3. C# API for Testing & Scripting

You can also spawn and command NPCs programmatically in C# from any node with access to [`GameRoot`](file:///Users/adrian/Code/godot/voxel-mcp/src/Game/Bootstrap/GameRoot.cs) or [`NpcManager`](file:///Users/adrian/Code/godot/voxel-mcp/src/Core/Npc/NpcManager.cs):

```csharp
using VoxelKing.Core.Common;
using VoxelKing.Game.Bootstrap;

// Retrieve GameRoot service container
var gameRoot = GetTree().Root.GetNode<GameRoot>("GameRoot");

// 1. Spawn an NPC
var npc = gameRoot.NpcManager.SpawnNpc(
    name: "Miner_Bob",
    position: new Vec3(10f, 65f, 5f),
    profile: "Collects iron ore"
);

// 2. Move NPC with velocity
npc.Velocity = new Vec3(3.0f, 0f, 0f);
npc.SetFacingDirection(npc.Velocity);

// 3. Make NPC jump
npc.Jump();

// 4. Despawn NPC
gameRoot.NpcManager.DespawnNpc(npc.Id);
```
