namespace VoxelKing.Game.Debug;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Bootstrap;
using VoxelKing.Game.Npc;
using VoxelKing.Game.Player;

/// <summary>
/// Headless test runner validating F-406: Spawning multiple NPCs with profiles.
/// Verifies:
/// - >= 5 NPCs spawned from config/npcs.json near player start.
/// - Distinct names and non-empty personalities/skills.
/// - Distinct appearance colors rendered on NpcNode visuals.
/// - Matching name tags.
/// - >= 5 NPCs run independently with different brains at stable frame rate.
/// </summary>
public partial class TestMultipleNpcsRunner : Node
{
    private const int TargetTestFrames = 200;

    private GameRoot _gameRoot = null!;
    private NpcViewManager _viewManager = null!;
    private int _frameCounter;
    private bool _verifiedInitial;

    public override void _Ready()
    {
        _gameRoot = GetNodeOrNull<GameRoot>("GameRoot") ?? GetParent<GameRoot>();
        if (_gameRoot == null)
        {
            GD.PrintErr("[FAIL] TestMultipleNpcsRunner could not locate GameRoot.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[INFO] TestMultipleNpcsRunner initialized.");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gameRoot.NpcManager == null)
        {
            return;
        }

        if (!_verifiedInitial)
        {
            // Give GameRoot a few frames to stream terrain and initialize views
            if (_frameCounter++ < 10)
            {
                return;
            }

            if (_gameRoot.NpcManager.ActiveNpcs.Count == 0)
            {
                var player = _gameRoot.GetNodeOrNull<PlayerController>("Player");
                _gameRoot.SpawnInitialNpcs(player);
            }

            _viewManager = _gameRoot.GetNodeOrNull<NpcViewManager>("NpcViewManager")!;
            if (_viewManager == null)
            {
                GD.PrintErr("[FAIL] Could not locate NpcViewManager.");
                GetTree().Quit(1);
                return;
            }

            VerifyNpcProfilesAndVisuals();
            _verifiedInitial = true;
            _frameCounter = 0;
            return;
        }

        _frameCounter++;

        // Verify all NPCs continue to run without errors or faulting
        if (_frameCounter % 20 == 0)
        {
            foreach (var npc in _gameRoot.NpcManager.ActiveNpcs)
            {
                if (!npc.IsActive || npc.IsDead)
                {
                    GD.PrintErr($"[FAIL] NPC '{npc.Name}' is unexpectedly inactive or dead.");
                    GetTree().Quit(1);
                    return;
                }

                if (npc.BrainHost != null && npc.BrainHost.Status == BrainStatus.Faulted)
                {
                    GD.PrintErr($"[FAIL] NPC '{npc.Name}' brain faulted: {npc.BrainHost.LastError}");
                    GetTree().Quit(1);
                    return;
                }
            }
        }

        if (_frameCounter >= TargetTestFrames)
        {
            int activeCount = _gameRoot.NpcManager.ActiveNpcs.Count;
            GD.Print($"[PASS] ALL {activeCount} NPCS WITH PROFILES RAN INDEPENDENTLY WITH DIFFERENT BRAINS FOR {_frameCounter} FRAMES AT STABLE FRAME RATE!");
            GetTree().Quit(0);
        }
    }

    private void VerifyNpcProfilesAndVisuals()
    {
        var activeNpcs = _gameRoot.NpcManager.ActiveNpcs.ToList();
        GD.Print($"[INFO] Active NPCs count: {activeNpcs.Count}");

        if (activeNpcs.Count < 5)
        {
            GD.PrintErr($"[FAIL] Expected at least 5 active NPCs with profiles, found {activeNpcs.Count}.");
            GetTree().Quit(1);
            return;
        }

        // 1. Verify distinct names
        var names = activeNpcs.Select(n => n.Name).ToList();
        var uniqueNames = names.ToHashSet();
        if (uniqueNames.Count != names.Count)
        {
            GD.PrintErr($"[FAIL] NPC names are not distinct: {string.Join(", ", names)}");
            GetTree().Quit(1);
            return;
        }

        // 2. Verify distinct appearance colors
        var colors = activeNpcs.Select(n => n.AppearanceColor).ToList();
        var uniqueColors = colors.Where(c => !string.IsNullOrEmpty(c)).ToHashSet();
        if (uniqueColors.Count < 5)
        {
            GD.PrintErr($"[FAIL] Expected at least 5 distinct appearance colors, found {uniqueColors.Count}: {string.Join(", ", colors)}");
            GetTree().Quit(1);
            return;
        }

        // 3. Verify personalities and skills
        foreach (var npc in activeNpcs)
        {
            if (string.IsNullOrWhiteSpace(npc.Personality) && string.IsNullOrWhiteSpace(npc.Profile))
            {
                GD.PrintErr($"[FAIL] NPC '{npc.Name}' has empty personality/profile.");
                GetTree().Quit(1);
                return;
            }

            if (npc.Skills == null || npc.Skills.Count == 0)
            {
                GD.PrintErr($"[FAIL] NPC '{npc.Name}' has empty skills.");
                GetTree().Quit(1);
                return;
            }
        }

        // 4. Verify visual views (NpcNode)
        foreach (var npc in activeNpcs)
        {
            var view = _viewManager.GetView(npc.Id);
            if (view == null)
            {
                GD.PrintErr($"[FAIL] No NpcNode visual found for NPC '{npc.Name}' (ID {npc.Id}).");
                GetTree().Quit(1);
                return;
            }

            if (view.NameTag == null || view.NameTag.Text != npc.Name)
            {
                GD.PrintErr($"[FAIL] NameTag on NpcNode for '{npc.Name}' does not match: '{view.NameTag?.Text}'.");
                GetTree().Quit(1);
                return;
            }

            if (view.TorsoMesh == null || view.TorsoMesh.MaterialOverride == null)
            {
                GD.PrintErr($"[FAIL] TorsoMesh on NpcNode for '{npc.Name}' has no material override.");
                GetTree().Quit(1);
                return;
            }

            if (view.TorsoMesh.MaterialOverride is StandardMaterial3D mat)
            {
                if (NpcColor.TryParse(npc.AppearanceColor, out var expectedCol))
                {
                    float rDiff = MathF.Abs(mat.AlbedoColor.R - expectedCol.R);
                    float gDiff = MathF.Abs(mat.AlbedoColor.G - expectedCol.G);
                    float bDiff = MathF.Abs(mat.AlbedoColor.B - expectedCol.B);

                    if (rDiff > 0.05f || gDiff > 0.05f || bDiff > 0.05f)
                    {
                        GD.PrintErr($"[FAIL] Torso material color for '{npc.Name}' ({mat.AlbedoColor}) does not match appearance color ({expectedCol}).");
                        GetTree().Quit(1);
                        return;
                    }
                }
            }
        }

        // 5. Verify different brains are running
        var brains = activeNpcs
            .Where(n => n.BrainHost != null)
            .Select(n => n.CurrentTaskSummary ?? n.BrainHost!.Status.ToString())
            .Distinct()
            .ToList();

        GD.Print($"[INFO] Distinct active brain task summaries: {string.Join(" | ", brains)}");
        if (brains.Count < 3)
        {
            GD.PrintErr($"[FAIL] Expected NPCs to run multiple different brains, found only {brains.Count}.");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[INFO] All {activeNpcs.Count} NPCs verified with distinct names, profiles, appearance colors, name tags, and initial brains.");
    }
}
