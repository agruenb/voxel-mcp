# Lua Brain API — Contract v1

> This is the **design specification** for the API exposed to NPC brain scripts.
> At runtime the authoritative description is generated from the `[LuaApi]` attributes on the C#
> bindings (see ARCHITECTURE §5.4). Implementers must keep both in sync; any change to this
> contract bumps `BRAIN_API_VERSION` and must be reflected in the prompt examples.

## 1. Script structure

A brain is a single Lua 5.2 chunk that **defines functions**; it must not perform work at top level
(top-level code is executed once with a tiny budget and may only define functions/locals).

```lua
-- Required: long-running behaviour. Runs as a coroutine. Blocking actions allowed.
function main()
  ...
end

-- Optional event handlers. Run to completion within a small budget.
-- They MUST NOT call blocking functions (doing so raises an error).
function on_heard(speaker_id, text) end      -- someone within hearing range said something
function on_attacked(attacker_id, damage) end
function on_player_nearby(player_id) end     -- a player entered 6-block range (edge-triggered)
function on_interrupted(reason) end          -- reflex layer pre-empted the brain
```

Lifecycle:

| Situation | Behaviour |
|---|---|
| Script assigned | `main()` started on next brain tick |
| `main()` returns, or `npc.done(summary)` called | Task finished → event `task_done` logged, NPC switches to idle layer |
| Runtime error / budget violation | Task aborted → event `brain_error` logged, NPC says a confused line, idle layer |
| Game loaded / `npc.restart()` | `main()` starts again from the top — **store progress in `memory`** |
| New instruction from the player | Old script discarded, new one starts |

## 2. Value types

| Type | Lua representation |
|---|---|
| `pos` | table `{x=, y=, z=}` with integer block coordinates. Create with `vec(x, y, z)`. Supports `+`, `-`, `==`, `:dist(other)`, `:offset(dx,dy,dz)`, `:tostring()` |
| `entity_id` | integer. Use `world.entity(id)` for an info snapshot |
| `block_id` / `item_id` | string, e.g. `"stone"`, `"oak_log"`, `"wheat_seeds"`. Unknown ids raise an error |
| `result` | blocking actions return `ok, reason` — `ok` is boolean, `reason` a string on failure |

Failure reasons (closed set): `"unreachable"`, `"stuck"`, `"too_far"`, `"no_item"`, `"inventory_full"`,
`"invalid_target"`, `"target_gone"`, `"occupied"`, `"interrupted"`, `"timeout"`.

## 3. API reference

### 3.1 `npc` — self (non-blocking queries)

| Function | Returns | Description |
|---|---|---|
| `npc.id()` | `entity_id` | Own id |
| `npc.name()` | string | Own name |
| `npc.pos()` | `pos` | Block position of the feet |
| `npc.home()` | `pos` | Home/spawn position |
| `npc.health()` | number 0..100 | Current health |
| `npc.inventory()` | table `{item_id = count}` | Copy of the inventory |
| `npc.count(item_id)` | integer | Number of items held |
| `npc.free_slots()` | integer | Free inventory slots |
| `npc.instructor()` | `entity_id` | Entity that gave the current instruction |

### 3.2 `npc` — blocking actions (only inside `main`)

| Function | Description |
|---|---|
| `npc.go_to(pos [, opts])` → ok, reason | Walk to `pos`. `opts.tolerance` (default 0 = exact cell, use ≥1 for targets inside blocks), `opts.timeout` seconds, `opts.allow_partial` |
| `npc.go_near(pos, dist)` → ok, reason | Walk until within `dist` blocks of `pos` |
| `npc.follow(entity_id, dist, seconds)` → ok, reason | Follow an entity for a duration (`seconds = 0` → until it is gone or interrupted) |
| `npc.mine(pos)` → ok, reason | Break the block at `pos` (must be within 4 blocks); drop goes into inventory |
| `npc.place(pos, block_id)` → ok, reason | Place a block from inventory (within 4 blocks, target must be air and adjacent to a solid block) |
| `npc.pick_up(entity_id)` → ok, reason | Pick up a dropped item entity (walks to it) |
| `npc.drop(item_id, count)` → ok, reason | Drop items at feet |
| `npc.give(entity_id, item_id, count)` → ok, reason | Hand items to an entity within 3 blocks |
| `npc.deposit(pos, item_id, count)` → ok, reason | Put items into the container at `pos` (within 4 blocks). `count = -1` → all |
| `npc.withdraw(pos, item_id, count)` → ok, reason | Take items from a container |
| `npc.wait(seconds)` → true | Idle for the given time (max 600) |

### 3.3 `npc` — instantaneous / control

| Function | Description |
|---|---|
| `npc.say(text)` | Speak (max 200 chars). Heard by entities within 16 blocks. Rate-limited to 1 per 2 s (extra calls are dropped) |
| `npc.look_at(pos)` | Turn head/body towards a position |
| `npc.done([summary])` | Mark the task as finished; ends the brain |
| `npc.restart()` | Restart `main()` from the top (callable from handlers) |
| `npc.interrupt()` | Cancel the currently running blocking action (it returns `false, "interrupted"`); callable from handlers |
| `log(...)` | Write to the NPC debug log (inspector overlay) |

### 3.4 `world` — perception (non-blocking, limited by perception radius)

| Function | Returns | Description |
|---|---|---|
| `world.block(pos)` | `block_id` or `nil` | Block at `pos`; `nil` if outside perception radius (24) |
| `world.find_block(block_id [, radius])` | `pos` or `nil` | Nearest block of that type (radius ≤ 24) |
| `world.find_blocks(block_id [, radius [, max]])` | list of `pos`, nearest first | `max` ≤ 64 |
| `world.find_entity(kind [, radius])` | `entity_id` or `nil` | Nearest entity of kind `"player"`, `"npc"`, `"item"`, `"animal"` (radius ≤ 32) |
| `world.find_entities(kind [, radius])` | list of `entity_id`, nearest first | |
| `world.entity(id)` | table or `nil` | `{id, kind, name, pos, health, item_id?, count?}`; `nil` if not perceivable |
| `world.is_walkable(pos)` | boolean | NPC could stand at `pos` |
| `world.surface(x, z)` | `pos` or `nil` | Highest walkable cell in column (within radius) |
| `world.time()` | number 0..24 | Time of day in hours |
| `world.is_day()` | boolean | |

### 3.5 `memory` — persistent per-NPC storage

| Function | Description |
|---|---|
| `memory.get(key [, default])` | Read a value |
| `memory.set(key, value)` | Store a number, string, boolean, `pos` or flat table of those. Limits: 64 keys, 16 KB total |
| `memory.place(name)` | `pos` of a named known place or `nil` (e.g. `"home"`, `"player_chest"`) |
| `memory.remember_place(name, pos)` | Store/overwrite a named place |
| `memory.places()` | list of known place names |

### 3.6 Utilities

`vec(x,y,z)`, `random(a, b)` (deterministic per NPC), `dist(a, b)`, Lua standard libs `math`,
`string` (without `dump`), `table`. **Not available:** `io`, `os`, `require`, `load*`, `dofile`,
`debug`, `coroutine`, `print` (use `log`).

## 4. Runtime limits

| Limit | Default | On violation |
|---|---|---|
| Instructions per brain tick (`main`) | 2 000 | Forced yield, continues next tick |
| Consecutive forced yields without any action | 50 | Error `"busy loop"` |
| Instructions per handler invocation | 5 000 | Error `"handler budget exceeded"` |
| Top-level chunk instructions | 10 000 | Validation error |
| Script size | 8 KB | Validation error |
| `say` length / rate | 200 chars / 1 per 2 s | Truncated / dropped |

## 5. Reference example

```lua
-- Instruction: "Collect 10 logs and put them in my chest"
local TARGET = 10

function main()
  local chest = memory.place("player_chest")
  if not chest then
    npc.say("I don't know where your chest is. Show me?")
    return
  end
  npc.say("Alright, I'll get you " .. TARGET .. " logs.")
  while (memory.get("delivered", 0)) < TARGET do
    local log_pos = world.find_block("oak_log", 24)
    if not log_pos then
      npc.say("No more trees nearby, looking further away.")
      local ok = npc.go_to(npc.pos():offset(random(-20, 20), 0, random(-20, 20)), {tolerance = 3, allow_partial = true})
      if not ok then npc.wait(2) end
    else
      if npc.go_near(log_pos, 2) then npc.mine(log_pos) end
    end
    local held = npc.count("oak_log")
    if held >= 5 or held + memory.get("delivered", 0) >= TARGET then
      if npc.go_near(chest, 2) and npc.deposit(chest, "oak_log", -1) then
        memory.set("delivered", memory.get("delivered", 0) + held)
      end
    end
  end
  npc.done("Delivered " .. TARGET .. " logs")
end

function on_attacked(attacker_id, damage)
  npc.say("Hey! Stop that!")
end
```

## 6. Extensibility rules

- New functions are added by writing a C# binding with `[LuaApi]`; the prompt picks it up automatically.
- Never expose live C# objects to Lua — only primitives, `pos` and copied tables.
- Every blocking action must have a timeout and must be cancellable.
- Reserved names for future features: `npc.ask(question)` (escalate to LLM), `npc.tell(entity_id, text)`
  (directed NPC↔NPC speech), `on_told(speaker_id, text)`.
