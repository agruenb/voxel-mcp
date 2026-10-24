# Feature Plan: Lively NPC Visuals & Animation System

## Status
- **Target Milestone:** M2 Polish / M4 Extension
- **Feature Code:** F-210 (NPC Visuals & Animation Polish)
- **Dependencies:** F-202 (NPC node, visuals & kinematic movement), F-405 (Dialogue UI & conversation states)
- **Status:** Proposed

---

## 1. Problem Statement
The current NPC visual representation ([NpcNode.cs](file:///Users/adrian/Code/godot/voxel-mcp/src/Game/Npc/NpcNode.cs)) renders a basic, rigid mannequin using 6 untextured `BoxMesh` boxes.
- **Static Face:** NPCs have completely blank faces without eyes, mouth, or expressions.
- **Rigid Gaze:** NPCs only rotate their full body along their movement heading. They do not turn their heads toward the player when spoken to or look around their environment.
- **Flat Motion:** Walking consists only of simple linear leg and arm sine waves without secondary motion (no banking/leaning into turns, no landing compression, no airborne jump pose).
- **No Handheld Items or Action Gestures:** NPCs execute tasks (digging, placing, talking, thinking) with static arms and empty hands.

---

## 2. Architecture & Boundary Rules
Following [docs/ARCHITECTURE.md](file:///Users/adrian/Code/godot/voxel-mcp/docs/ARCHITECTURE.md):
- **Core remains pure:** No Godot imports in `src/Core`. All mesh instantiation, materials, Godot animation timers, scene graph transforms, and IK/look-at math live strictly within `src/Game/Npc/` (`NpcNode.cs`, `NpcViewManager.cs`).
- **Data flow:** Visual state derives from authoritative [NpcState](file:///Users/adrian/Code/godot/voxel-mcp/src/Core/Npc/NpcState.cs) (position, velocity, facing, conversation state, equipped/active item, current task).
- **Interpolation:** Simulation runs at fixed tick; visual smoothing and procedural micro-animations occur in `_Process(double delta)`.

---

## 3. Scope & Phased Implementation

### Phase 1: Expressive Head & Facial Features (Implemented)
1. **Facial Decal / Expressive Face Quad:**
   - Attach a low-poly face quad / pixel-art facial texture to the front of `HeadMesh`.
   - Render eyes (sclera + pupils) and mouth.
   - **Blink system:** Periodic random timer (every 2.5–5s) triggering a brief eye blink (scaling eye quad vertically to zero for ~100ms).
   - **Expression states:**
     - `Default`: Neutral friendly gaze.
     - `Thinking`: One eyebrow raised / squinted eyes when `NpcConversationState.Thinking`.
     - `Talking`: Slight vertical mouth oscillation when speech bubble is visible.

2. **Gaze & Head-Tracking (Look-At IK):**
   - Head yaw and pitch rotate independently from the torso.
   - When the player is within range (< 4.5m) and conversation/dialogue is active (or player is looked at), smoothly interpolate `HeadMesh.Rotation` toward the player's camera position.
   - Clamp rotation limits to natural human ranges (Yaw: ±55°, Pitch: ±30°).
   - If no target is nearby, periodically glance around subtly (small randomized idle head offsets).

### Phase 2: Locomotion Juice & Secondary Motion (Implemented)
1. **Turn Banking (Torso Roll/Lean):**
   - Calculate angular turning velocity $(\Delta\text{Facing} / \Delta t)$.
   - Lean the torso and head slightly into the turn (Roll / Yaw lag) for fluid momentum.
2. **Airborne & Jump Dynamics:**
   - Detect `!NpcState.IsOnGround` and vertical velocity $V_y$:
     - Ascending ($V_y > 0$): Arms raise slightly, legs extend downwards.
     - Falling ($V_y < 0$): Arms flare outwards for balance.
3. **Landing Impact (Squash & Stretch):**
   - Detect the exact transition from `!IsOnGround` to `IsOnGround`.
   - Apply a subtle vertical squash (`Visuals.Scale = (1.08, 0.92, 1.08)`) bouncing smoothly back to $(1, 1, 1)$ over 150ms.

### Phase 3: Action Gestures & Handheld Items (Implemented)
1. **Hand Attachment Socket (`RightHandSlot`):**
   - Add a marker node under `RightArmMesh` representing the grip position.
   - Read the currently active or selected item/block from `NpcState.Inventory` (or profile starting item).
   - Render a miniature 3D voxel block or tool model in the hand.
2. **Action Gestures:**
   - **Dig / Place / Attack Swing:** Rapid forward-and-back hammer swing of the right arm when an action is active (inspecting task state or action events).
   - **Conversation Gestures:** Occasional subtle hand gesticulation (small arm swing offset) when speaking in dialogue.
   - **Thinking Pose:** Right arm raised scratching head when conversation state is `Thinking`.

### Phase 4: Archetype Customization & Distinct Silhouettes (Implemented)
1. **Hair & Headgear Layer:**
   - Procedural hair block / cap layer surrounding top, back, and sides of head (colored from profile data or archetype).
   - Optional accessory block (e.g. farmer straw hat, miner helmet, headband, courier cap).
2. **Clothing Details:**
   - Add secondary tone for boots/shoes and sleeve cuffs to give depth beyond single solid color blocks.

---

## 4. Acceptance Criteria
- [x] NPC head smoothly turns to track the player when within conversation distance (< 4.5m), respecting neck rotation angle limits. (Phase 1)
- [x] NPCs blink periodically (eyes close for ~0.1s every 3–6s). (Phase 1)
- [x] NPC shows an expressive thinking pose/facial cue when LLM inference is running (`NpcConversationState.Thinking`). (Phase 1)
- [x] NPC body subtly banks into turns and exhibits squash/stretch on landing after falling or jumping. (Phase 2)
- [x] NPC holds active tool/item in their hand during tasks and displays an arm swinging animation while digging/building. (Phase 3)
- [x] NPC models display archetype/profile customization: hair/hat layers and clothing accents (boots/cuffs). (Phase 4)
- [x] Core tests continue to pass (`dotnet test`) and visual node logic remains strictly in `src/Game`. (Phase 1, 2, 3 & 4)
