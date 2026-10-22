namespace VoxelKing.Game.Debug;

using System;
using System.Collections.Generic;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Bootstrap;

/// <summary>
/// Headless test runner verifying that all valid example brains run in-game without errors (F-305).
/// </summary>
public partial class TestBrainExamplesRunner : Node
{
    [Export]
    public int TestFrames { get; set; } = 300;

    private GameRoot _gameRoot = null!;
    private readonly List<(NpcState Npc, BrainExample Example)> _runningNpcs = new();
    private int _frameCounter = 0;
    private bool _initialized = false;

    public override void _Ready()
    {
        _gameRoot = GetNodeOrNull<GameRoot>("GameRoot") ?? GetParent<GameRoot>();
        if (_gameRoot == null)
        {
            GD.PrintErr("[FAIL] TestBrainExamplesRunner could not find GameRoot node.");
            GetTree().Quit(1);
            return;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized)
        {
            // Wait 10 frames for initial terrain generation to settle
            _frameCounter++;
            if (_frameCounter < 10)
            {
                return;
            }

            InitializeNpcs();
            _initialized = true;
            _frameCounter = 0;
            return;
        }

        _frameCounter++;

        // Periodically verify all brains are healthy
        if (_frameCounter % 15 == 0)
        {
            foreach (var (npc, example) in _runningNpcs)
            {
                if (npc.BrainHost == null)
                {
                    GD.PrintErr($"[FAIL] NPC '{npc.Name}' has null BrainHost.");
                    GetTree().Quit(1);
                    return;
                }

                if (npc.BrainHost.Status == BrainStatus.Faulted)
                {
                    GD.PrintErr($"[FAIL] Brain '{example.Name}' faulted on NPC '{npc.Name}': {npc.BrainHost.LastError}");
                    GetTree().Quit(1);
                    return;
                }
            }
        }

        if (_frameCounter >= TestFrames)
        {
            GD.Print($"[PASS] ALL {_runningNpcs.Count} EXAMPLE BRAINS RAN SUCCESSFULLY IN-GAME FOR {_frameCounter} FRAMES WITHOUT ERRORS!");
            GetTree().Quit(0);
        }
    }

    private void InitializeNpcs()
    {
        string examplesDir = ProjectSettings.GlobalizePath("res://brains/examples");
        var allExamples = BrainExampleLibrary.LoadFromDirectory(examplesDir);

        int spawned = 0;
        foreach (var example in allExamples)
        {
            if (!example.IsValid)
            {
                continue;
            }

            float offsetX = (spawned - 3) * 3.0f;
            float offsetZ = 5.0f + (spawned % 2) * 2.0f;
            float surfaceY = _gameRoot.TerrainGenerator.GetHeight((int)offsetX, (int)offsetZ);
            var spawnPos = new Vec3(offsetX, surfaceY + 1.0f, offsetZ);

            var npc = _gameRoot.NpcManager.SpawnNpc(
                $"npc_{example.Name}",
                spawnPos,
                home: spawnPos);

            // Add basic inventory items if needed
            npc.Inventory?.AddItem("stone", 10, out _);
            npc.Inventory?.AddItem("oak_log", 10, out _);

            if (npc.BrainHost == null)
            {
                GD.PrintErr($"[FAIL] Failed to initialize BrainHost for NPC 'npc_{example.Name}'.");
                GetTree().Quit(1);
                return;
            }

            bool loaded = npc.BrainHost.LoadScript(example.Script, _gameRoot.PlayerEntity?.Id);
            if (!loaded)
            {
                GD.PrintErr($"[FAIL] Failed to load script for example '{example.Name}': {npc.BrainHost.LastError}");
                GetTree().Quit(1);
                return;
            }

            _runningNpcs.Add((npc, example));
            GD.Print($"[INFO] Spawned NPC 'npc_{example.Name}' running example brain '{example.FileName}' at {spawnPos}");
            spawned++;
        }

        GD.Print($"[INFO] Initialized {_runningNpcs.Count} NPCs with valid example brains.");
    }
}
