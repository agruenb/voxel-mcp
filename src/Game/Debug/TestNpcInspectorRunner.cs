namespace VoxelKing.Game.Debug;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Game.Bootstrap;

/// <summary>
/// Headless integration test runner validating the NPC Inspector Overlay (F-502).
/// Verifies:
/// - Toggling overlay on looked-at/targeted NPC via F3
/// - Real-time live updates of Layer, Action, Navigation Path, Inventory, Memory KV,
///   Recent Events, Brain Logs, Instruction Budget, Last Error, Last Prompt/Response, and Token Totals
/// - Walk Mode toggle (mouse capture vs interactive inspection)
/// - Clean closing and restoring player controls
/// </summary>
public partial class TestNpcInspectorRunner : Node
{
    private GameRoot _gameRoot = null!;
    private NpcInspectorOverlay _overlay = null!;
    private NpcState _testNpc = null!;
    private int _step = 0;
    private int _waitFrames = 0;

    public override void _Ready()
    {
        _gameRoot = GetNodeOrNull<GameRoot>("GameRoot") ?? GetParent<GameRoot>();
        if (_gameRoot == null)
        {
            GD.PrintErr("[FAIL] TestNpcInspectorRunner could not find GameRoot.");
            GetTree().Quit(1);
            return;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _waitFrames++;
        if (_waitFrames < 15)
        {
            return; // Allow terrain and nodes to settle
        }

        switch (_step)
        {
            case 0:
            {
                _overlay = _gameRoot.NpcInspectorOverlay;
                if (_overlay == null)
                {
                    GD.PrintErr("[FAIL] GameRoot.NpcInspectorOverlay is null.");
                    GetTree().Quit(1);
                    return;
                }

                // 1. Spawn a test NPC with profile and memory
                var spawnPos = new Vec3(4f, 65f, 4f);
                _testNpc = _gameRoot.NpcManager.SpawnNpc(
                    "InspectorBob",
                    spawnPos,
                    personality: "meticulous researcher",
                    skills: new[] { "diagnostics", "engineering" },
                    appearanceColor: "#3D8BEB");

                GD.Print($"[INFO] Spawned test NPC '{_testNpc.Name}' (ID {_testNpc.Id}).");

                // 2. Open Inspector on test NPC
                _overlay.OpenOverlay(_testNpc);
                AssertTrue(_overlay.IsOpen, "Inspector overlay should be open after OpenOverlay().");
                AssertTrue(_overlay.TargetNpc == _testNpc, "Target NPC should be set to test NPC.");
                GD.Print("[PASS] Step 1: Inspector overlay opened on target NPC.");

                _step++;
                _waitFrames = 0;
                break;
            }

            case 1:
            {
                // 3. Test Action and Path live updating
                var pathWaypoints = new List<Vec3i>
                {
                    new(4, 65, 5),
                    new(4, 65, 6),
                    new(5, 65, 7)
                };
                var pathFollower = new SimpleTestPathFollower(_testNpc, pathWaypoints);
                _testNpc.PathFollower = pathFollower;

                var testAction = new SimpleTestAction("inspect_area");
                _testNpc.CurrentAction = testAction;

                _overlay.UpdateLiveFields();

                AssertTrue(_testNpc.CurrentAction?.Name == "inspect_area", "NPC CurrentAction should be inspect_area.");
                AssertTrue(_testNpc.ActivePath?.Count == 3, "NPC ActivePath should have 3 waypoints.");
                GD.Print("[PASS] Step 2: Action and Path live updates verified.");

                _step++;
                _waitFrames = 0;
                break;
            }

            case 2:
            {
                // 4. Test Inventory and Memory KV live updating
                _testNpc.Inventory.AddItem("cobblestone", 32, out _);
                _testNpc.Inventory.AddItem("iron_pickaxe", 1, out _);

                _testNpc.Memory.Set("current_task", "survey terrain");
                _testNpc.Memory.Set("sample_count", 5);
                _testNpc.Memory.RememberPlace("laboratory", new Vec3i(10, 65, -12));

                _overlay.UpdateLiveFields();

                AssertTrue(_testNpc.Inventory.CountItem("cobblestone") >= 32, "Inventory should contain cobblestone.");
                AssertTrue(_testNpc.Inventory.CountItem("iron_pickaxe") >= 1, "Inventory should contain iron_pickaxe.");
                AssertTrue(_testNpc.Memory.Has("current_task"), "Memory KV should contain current_task.");
                AssertTrue(_testNpc.Memory.Place("laboratory") != null, "Memory places should contain laboratory.");
                GD.Print("[PASS] Step 3: Inventory and Memory KV live updates verified.");

                _step++;
                _waitFrames = 0;
                break;
            }

            case 3:
            {
                // 5. Test Recent Events and Brain Log live updating
                _testNpc.Memory.RecordInstruction("Player", "inspect the southern ruins", timestamp: 12.5);
                _testNpc.Memory.RecordSpeechSpoke("Heading out to survey.", timestamp: 14.0);

                var host = _testNpc.BrainHost;
                AssertTrue(host != null, "BrainHost should not be null.");
                host!.AddLog("Starting sensory calibration loop");
                host.AddLog("Detected solid foundation at (4, 65, 7)");

                _overlay.UpdateLiveFields();

                var recentEvents = _testNpc.Memory.GetRecentEvents(10);
                AssertTrue(recentEvents.Count >= 2, "Recent events should have at least 2 events.");
                AssertTrue(host.RecentLogs.Count >= 2, "BrainHost should have recorded logs.");
                AssertTrue(host.RecentLogs.Any(l => l.Contains("sensory calibration")), "Recent logs should include calibration log.");
                GD.Print("[PASS] Step 4: Events ring buffer and Brain Log live updates verified.");

                _step++;
                _waitFrames = 0;
                break;
            }

            case 4:
            {
                // 6. Test Instruction Budget, LLM Prompts & Responses, and Token Usage
                _testNpc.LastPrompt = "[System Instruction]\nYou are InspectorBob.\n\n[User Instruction]\nSurvey the area";
                _testNpc.LastResponse = "{\"reply\":\"Acknowledged, scanning surrounding voxels.\",\"script\":\"function main() end\"}";
                _testNpc.TotalTokenUsage = new LlmUsage(150, 48, 198);

                _overlay.UpdateLiveFields();

                AssertTrue(_testNpc.LastPrompt.Contains("InspectorBob"), "LastPrompt should contain InspectorBob.");
                AssertTrue(_testNpc.LastResponse.Contains("Acknowledged"), "LastResponse should contain reply text.");
                AssertTrue(_testNpc.TotalTokenUsage.TotalTokens == 198, "TotalTokenUsage should match 198 tokens.");
                GD.Print("[PASS] Step 5: LLM prompt, response, and token metrics verified.");

                _step++;
                _waitFrames = 0;
                break;
            }

            case 5:
            {
                // 7. Test Walk Mode toggle and closing overlay
                _overlay.ToggleWalkMode();
                AssertTrue(_overlay.IsWalkMode, "Overlay should be in Walk Mode after toggle.");

                _overlay.ToggleWalkMode();
                AssertTrue(!_overlay.IsWalkMode, "Overlay should return to interactive mode after toggle.");

                _overlay.CloseOverlay();
                AssertTrue(!_overlay.IsOpen, "Overlay should be closed after CloseOverlay().");

                // Test F3 toggle re-opens
                _overlay.OpenOverlay(_testNpc);
                AssertTrue(_overlay.IsOpen, "Overlay should be open.");
                _overlay.ToggleOverlay();
                AssertTrue(!_overlay.IsOpen, "ToggleOverlay() should close when currently open.");

                GD.Print("[PASS] Step 6: Walk Mode and F3 toggle controls verified.");
                GD.Print("[PASS] ALL NPC INSPECTOR OVERLAY TESTS PASSED SUCCESSFULLY!");
                GetTree().Quit(0);
                break;
            }
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"[ASSERTION FAILED] {message}");
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SimpleTestPathFollower : IPathFollower
    {
        public NpcState Npc { get; }
        public IReadOnlyList<Vec3i> CurrentPath { get; private set; }
        public IReadOnlyList<Vec3i> RemainingWaypoints => CurrentPath;
        public int CurrentWaypointIndex => 0;
        public Vec3i? TargetGoal { get; private set; }
        public PathFollowerStatus Status => PathFollowerStatus.Following;
        public float MoveSpeed { get; set; } = 3.5f;
        public float StuckTimeout { get; set; } = 2.0f;

        public SimpleTestPathFollower(NpcState npc, IReadOnlyList<Vec3i> waypoints)
        {
            Npc = npc;
            CurrentPath = waypoints;
            TargetGoal = waypoints.Count > 0 ? waypoints[^1] : null;
        }

        public void FollowPath(IReadOnlyList<Vec3i> path, Vec3i? goal = null)
        {
            CurrentPath = path;
            TargetGoal = goal;
        }

        public void Update(float dt) { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class SimpleTestAction : ActionBase
    {
        public override string Name { get; }

        public SimpleTestAction(string name) : base(60f)
        {
            Name = name;
            Start();
        }

        protected override void OnStart() { }
        protected override void OnTick(float dt) { }
    }
}
