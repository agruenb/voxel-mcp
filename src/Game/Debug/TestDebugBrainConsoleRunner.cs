namespace VoxelKing.Game.Debug;

using System;
using System.IO;
using System.Linq;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Bootstrap;

/// <summary>
/// Headless integration test validating the Debug Brain Console (F-306).
/// Verifies selecting NPCs, loading and applying all example brains at runtime through the validator,
/// rejecting invalid scripts with error reporting, and viewing brain logs.
/// </summary>
public partial class TestDebugBrainConsoleRunner : Node
{
    private GameRoot _gameRoot = null!;
    private DebugBrainConsole _console = null!;
    private NpcState _testNpc = null!;
    private int _step = 0;
    private int _waitFrames = 0;

    public override void _Ready()
    {
        _gameRoot = GetNodeOrNull<GameRoot>("GameRoot") ?? GetParent<GameRoot>();
        if (_gameRoot == null)
        {
            GD.PrintErr("[FAIL] TestDebugBrainConsoleRunner could not find GameRoot.");
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
                _console = _gameRoot.DebugBrainConsole;
                if (_console == null)
                {
                    GD.PrintErr("[FAIL] GameRoot.DebugBrainConsole is null.");
                    GetTree().Quit(1);
                    return;
                }

                // 1. Spawn a test NPC
                var spawnPos = new Vec3(2f, 65f, 2f);
                _testNpc = _gameRoot.NpcManager.SpawnNpc("TestNpc_Alice", spawnPos, home: spawnPos);
                GD.Print($"[INFO] Spawned test NPC '{_testNpc.Name}' (ID {_testNpc.Id}).");

                // 2. Open Console
                _console.OpenConsole();
                AssertTrue(_console.IsOpen, "Console should be open after OpenConsole()");
                GD.Print("[PASS] Step 1: Brain console opened successfully.");

                // 3. Select test NPC
                _console.SelectNpc(_testNpc);
                _step++;
                _waitFrames = 0;
                break;
            }

            case 1:
            {
                // Test applying each valid example brain to the NPC
                string examplesDir = ProjectSettings.GlobalizePath("res://brains/examples");
                var examples = BrainExampleLibrary.LoadFromDirectory(examplesDir).Where(e => e.IsValid).ToList();
                AssertTrue(examples.Count >= 7, "Expected at least 7 valid example brains.");

                foreach (var example in examples)
                {
                    _console.ScriptEditor.Text = example.Script;

                    // Apply to NPC through validator
                    _console.ApplyCurrentScript();

                    AssertTrue(_testNpc.BrainHost != null, "BrainHost should not be null.");
                    AssertTrue(_testNpc.BrainHost!.Status == BrainStatus.Running,
                        $"Brain status for '{example.Name}' should be Running, but was {_testNpc.BrainHost.Status}");
                    AssertTrue(_testNpc.BrainHost.CurrentScript == example.Script,
                        $"Brain current script should match '{example.Name}' source.");

                    GD.Print($"[PASS] Successfully validated and applied example brain '{example.FileName}' to NPC '{_testNpc.Name}' at runtime.");
                }

                _step++;
                _waitFrames = 0;
                break;
            }

            case 2:
            {
                // Test applying an invalid script: should be rejected by validator
                string invalidSyntax = "function main(\n  npc.say('broken syntax')\n";
                _console.ScriptEditor.Text = invalidSyntax;

                string previousScript = _testNpc.BrainHost!.CurrentScript!;
                _console.ApplyCurrentScript();

                // Current script should NOT have changed to the invalid script
                AssertTrue(_testNpc.BrainHost.CurrentScript == previousScript,
                    "Invalid script should not have been applied to NPC.");

                AssertTrue(_console.ValidationOutput.Text.Contains("Validation Failed"),
                    "Validation output should report validation failure for broken syntax.");

                GD.Print("[PASS] Step 2: Invalid brain script correctly rejected by validator and not applied to NPC.");

                _step++;
                _waitFrames = 0;
                break;
            }

            case 3:
            {
                // Let NPC tick with a valid script and check recent logs
                var brainHost = _testNpc.BrainHost;
                AssertTrue(brainHost != null, "BrainHost should not be null.");
                brainHost!.AddLog("Test runtime log message from integration test");
                var logs = brainHost.RecentLogs;
                AssertTrue(logs.Count > 0, "BrainHost should have recent logs.");
                AssertTrue(logs.Any(l => l.Contains("Test runtime log message")),
                    "Recent logs should contain the added test log.");

                GD.Print("[PASS] Step 3: NPC brain logs correctly recorded and viewable.");

                // Close Console
                _console.CloseConsole();
                AssertTrue(!_console.IsOpen, "Console should be closed after CloseConsole().");

                GD.Print("[PASS] ALL DEBUG BRAIN CONSOLE TESTS PASSED SUCCESSFULLY!");
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
}
