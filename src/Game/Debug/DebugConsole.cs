namespace VoxelKing.Game.Debug;

using System;
using System.Linq;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Navigation;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Player;

/// <summary>
/// Debug command console and shortcut handler for spawning, moving, jumping, and inspecting NPCs at runtime.
/// </summary>
public partial class DebugConsole : CanvasLayer
{
    private PanelContainer _panel = null!;
    private LineEdit _input = null!;
    private RichTextLabel _output = null!;
    private bool _isOpen;

    public NpcManager NpcManager { get; set; } = null!;
    public IVoxelWorld VoxelWorld { get; set; } = null!;
    public PlayerController? Player { get; set; }
    public IPathfindingService? PathfindingService { get; set; }
    public DebugPathRenderer? PathRenderer { get; set; }

    public bool IsOpen => _isOpen;

    public override void _Ready()
    {
        Layer = 100; // Above HUD
        BuildUI();
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        // Toggle Console with F1, Tilde (QuoteLeft)
        if (keyEvent.Keycode == Key.F1 || keyEvent.Keycode == Key.Quoteleft)
        {
            ToggleConsole();
            GetViewport().SetInputAsHandled();
            return;
        }

        // If console is open, handle Esc or Enter
        if (_isOpen)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                CloseConsole();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        // Shortcut debug hotkeys when console is closed:
        // F4: Quick-spawn NPC in front of player
        if (keyEvent.Keycode == Key.F4)
        {
            var npc = SpawnDebugNpc();
            Log($"[Quick-Spawn] Spawned NPC '{npc.Name}' (ID {npc.Id}) at {npc.Position}");
            GetViewport().SetInputAsHandled();
        }
        // F5: Move nearest NPC forward
        else if (keyEvent.Keycode == Key.F5)
        {
            var nearest = FindNearestNpc();
            if (nearest != null)
            {
                // Move forward in the direction player is looking or +X
                float vx = 3.0f;
                float vz = 0f;
                if (Player != null)
                {
                    var fwd = -Player.GlobalTransform.Basis.Z;
                    vx = fwd.X * 3.0f;
                    vz = fwd.Z * 3.0f;
                }
                MoveNpc(nearest.Id, vx, vz);
                nearest.SetFacingDirection(new Vec3(vx, 0f, vz));
                Log($"[Quick-Move] Moving NPC '{nearest.Name}' with velocity ({vx:F1}, {vz:F1})");
            }
            else
            {
                Log("[Quick-Move] No active NPCs found to move.");
            }
            GetViewport().SetInputAsHandled();
        }
        // F6: Make nearest NPC jump
        else if (keyEvent.Keycode == Key.F6)
        {
            var nearest = FindNearestNpc();
            if (nearest != null)
            {
                bool jumped = JumpNpc(nearest.Id);
                Log($"[Quick-Jump] NPC '{nearest.Name}' jump {(jumped ? "executed" : "failed (not on ground)")}");
            }
            GetViewport().SetInputAsHandled();
        }
        // F7: Stop nearest NPC
        else if (keyEvent.Keycode == Key.F7)
        {
            var nearest = FindNearestNpc();
            if (nearest != null)
            {
                StopNpc(nearest.Id);
                Log($"[Quick-Stop] Stopped NPC '{nearest.Name}'");
            }
            GetViewport().SetInputAsHandled();
        }
        // F8: Navigate nearest NPC to player targeted block or player position
        else if (keyEvent.Keycode == Key.F8)
        {
            var nearest = FindNearestNpc();
            if (nearest != null)
            {
                Vec3i goalCell;
                if (Player != null && Player.CurrentRaycast.IsHit)
                {
                    goalCell = Player.CurrentRaycast.AdjacentPos;
                }
                else if (Player != null)
                {
                    goalCell = new Vec3i(
                        (int)MathF.Floor(Player.GlobalPosition.X),
                        (int)MathF.Round(Player.GlobalPosition.Y),
                        (int)MathF.Floor(Player.GlobalPosition.Z));
                }
                else
                {
                    goalCell = new Vec3i(0, 65, 0);
                }

                if (NavigateNpc(nearest.Id, goalCell, out var result))
                {
                    Log($"[Quick-Nav] Navigating '{nearest.Name}' to {goalCell}. Waypoints: {result.Waypoints.Count} ({result.Status})");
                }
                else
                {
                    Log($"[Quick-Nav] Failed to find path for '{nearest.Name}' to {goalCell}: {result.FailureReason}");
                }
            }
            else
            {
                Log("[Quick-Nav] No active NPCs found to navigate.");
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleConsole()
    {
        if (_isOpen)
        {
            CloseConsole();
        }
        else
        {
            OpenConsole();
        }
    }

    public void OpenConsole()
    {
        _isOpen = true;
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (Player != null)
        {
            Player.IsInputLocked = true;
        }
        _input.GrabFocus();
    }

    public void CloseConsole()
    {
        _isOpen = false;
        Visible = false;
        _input.ReleaseFocus();
        if (Player != null)
        {
            Player.IsInputLocked = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public void Log(string message)
    {
        _output.AppendText(message + "\n");
        GD.Print($"[DebugConsole] {message}");
    }

    public NpcState SpawnDebugNpc(string name = "Bob", Vec3? position = null)
    {
        Vec3 spawnPos;
        if (position.HasValue)
        {
            spawnPos = position.Value;
        }
        else if (Player != null)
        {
            // Spawn 3 meters in front of the player
            var fwd = -Player.GlobalTransform.Basis.Z;
            var target = Player.GlobalPosition + (fwd * 3.0f);
            spawnPos = new Vec3(target.X, target.Y + 0.5f, target.Z);
        }
        else
        {
            spawnPos = new Vec3(0f, 65f, 0f);
        }

        int count = NpcManager.ActiveNpcs.Count + 1;
        string npcName = name == "Bob" && count > 1 ? $"Bob_{count}" : name;
        return NpcManager.SpawnNpc(npcName, spawnPos);
    }

    public bool MoveNpc(EntityId id, float vx, float vz)
    {
        if (NpcManager.Registry.TryGet<NpcState>(id, out var npc))
        {
            npc.Velocity = new Vec3(vx, npc.Velocity.Y, vz);
            return true;
        }
        return false;
    }

    public bool JumpNpc(EntityId id)
    {
        if (NpcManager.Registry.TryGet<NpcState>(id, out var npc))
        {
            return npc.Jump();
        }
        return false;
    }

    public bool StopNpc(EntityId id)
    {
        if (NpcManager.Registry.TryGet<NpcState>(id, out var npc))
        {
            npc.PathFollower?.Stop();
            npc.Velocity = new Vec3(0f, npc.Velocity.Y, 0f);
            return true;
        }
        return false;
    }

    public bool NavigateNpc(EntityId id, Vec3i targetCell, out PathResult result)
    {
        if (!NpcManager.Registry.TryGet<NpcState>(id, out var npc))
        {
            result = PathResult.Failed(PathFailureReason.NoPathFound);
            return false;
        }

        var startCell = new Vec3i(
            (int)MathF.Floor(npc.Position.X),
            (int)MathF.Round(npc.Position.Y),
            (int)MathF.Floor(npc.Position.Z));

        // If start cell is not walkable, try adjacent/surface
        if (!Walkability.IsWalkable(VoxelWorld, startCell))
        {
            var surface = Walkability.Surface(VoxelWorld, startCell.X, startCell.Z, startCell.Y + 2, startCell.Y - 2);
            if (surface.HasValue)
            {
                startCell = surface.Value;
            }
        }

        // If target cell is not walkable, try surface column
        var goalCell = targetCell;
        if (!Walkability.IsWalkable(VoxelWorld, goalCell))
        {
            var surface = Walkability.Surface(VoxelWorld, goalCell.X, goalCell.Z, goalCell.Y + 3, goalCell.Y - 3);
            if (surface.HasValue)
            {
                goalCell = surface.Value;
            }
        }

        var service = PathfindingService ?? new PathfindingService(VoxelWorld);
        result = service.FindPath(startCell, goalCell);

        if (result.HasPath)
        {
            npc.PathFollower?.Dispose();
            var follower = new PathFollower(npc, VoxelWorld, NpcManager.EventBus);
            follower.FollowPath(result.Waypoints, goalCell);
            npc.PathFollower = follower;
            return true;
        }

        return false;
    }

    public void ExecuteCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        string trimmed = commandLine.Trim();
        if (trimmed.StartsWith('/'))
        {
            trimmed = trimmed[1..];
        }

        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        string cmd = parts[0].ToLowerInvariant();

        switch (cmd)
        {
            case "help":
                Log("Available commands:");
                Log("  spawn_npc [name]          - Spawn an NPC in front of player");
                Log("  move_npc <id|name> <vx> <vz> - Move an NPC with velocity");
                Log("  jump_npc <id|name>        - Make an NPC jump");
                Log("  stop_npc <id|name>        - Stop an NPC");
                Log("  nav_to <id|name> [x y z]  - Navigate an NPC to coordinates or target block");
                Log("  mine_npc <id|name> [x y z]- Mine block at coordinates");
                Log("  walk_and_mine <id|name> [x y z] - Walk near block and mine it");
                Log("  place_npc <id|name> <blk> [x y z] - Place block from NPC inventory");
                Log("  say_npc <id|name> <text>  - Make NPC speak");
                Log("  wait_npc <id|name> <secs> - Make NPC wait");
                Log("  cancel_npc <id|name>      - Cancel active action on NPC");
                Log("  list_npcs                 - List all active NPCs");
                Log("  brain                     - Toggle Lua Brain Console (F2)");
                Log("Hotkeys: F2=BrainConsole, F4=Spawn, F5=Move, F6=Jump, F7=Stop, F8=NavToLook");
                break;

            case "brain":
            case "brain_console":
            {
                var brainConsole = GetParent()?.GetNodeOrNull<DebugBrainConsole>("DebugBrainConsole");
                if (brainConsole != null)
                {
                    brainConsole.ToggleConsole();
                    Log("Toggled Lua Brain Console (F2).");
                }
                else
                {
                    Log("DebugBrainConsole node not found.");
                }
                break;
            }

            case "spawn_npc":
            case "spawn":
            {
                string name = parts.Length > 1 ? parts[1] : "Bob";
                var npc = SpawnDebugNpc(name);
                Log($"Spawned NPC '{npc.Name}' (ID {npc.Id}) at {npc.Position}");
                break;
            }

            case "move_npc":
            case "move":
            {
                if (parts.Length < 4)
                {
                    Log("Usage: move_npc <id|name> <vx> <vz>");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                if (float.TryParse(parts[2], out float vx) && float.TryParse(parts[3], out float vz))
                {
                    MoveNpc(target.Id, vx, vz);
                    target.SetFacingDirection(new Vec3(vx, 0f, vz));
                    Log($"Set NPC '{target.Name}' (ID {target.Id}) velocity to ({vx:F1}, {vz:F1})");
                }
                else
                {
                    Log("Invalid velocity numbers. Usage: move_npc <id|name> <vx> <vz>");
                }
                break;
            }

            case "jump_npc":
            case "jump":
            {
                if (parts.Length < 2)
                {
                    Log("Usage: jump_npc <id|name>");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                bool ok = JumpNpc(target.Id);
                Log($"NPC '{target.Name}' (ID {target.Id}) jump {(ok ? "successful" : "failed (not on ground)")}");
                break;
            }

            case "stop_npc":
            case "stop":
            {
                if (parts.Length < 2)
                {
                    Log("Usage: stop_npc <id|name>");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                StopNpc(target.Id);
                Log($"Stopped NPC '{target.Name}' (ID {target.Id})");
                break;
            }

            case "list_npcs":
            case "list":
            {
                var list = NpcManager.ActiveNpcs.ToList();
                Log($"Active NPCs ({list.Count}):");
                foreach (var npc in list)
                {
                    string navStatus = npc.PathFollower != null ? $", Nav={npc.PathFollower.Status}" : "";
                    string layerStr = npc.Arbiter != null ? $", Layer={npc.Arbiter.ActiveLayer}" : "";
                    string actionStr = npc.CurrentAction != null ? $", Action={npc.CurrentAction.Name}" : "";
                    Log($"  ID {npc.Id}: '{npc.Name}' at {npc.Position} (Grounded={npc.IsOnGround}, Wall={npc.IsOnWall}, LOD={npc.LodTier}{layerStr}{actionStr}{navStatus})");
                }
                break;
            }

            case "nav_to":
            case "goto":
            {
                if (parts.Length < 2)
                {
                    Log("Usage: nav_to <id|name> [x y z]");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                Vec3i goalCell;
                if (parts.Length >= 5 &&
                    int.TryParse(parts[2], out int gx) &&
                    int.TryParse(parts[3], out int gy) &&
                    int.TryParse(parts[4], out int gz))
                {
                    goalCell = new Vec3i(gx, gy, gz);
                }
                else if (Player != null && Player.CurrentRaycast.IsHit)
                {
                    goalCell = Player.CurrentRaycast.AdjacentPos;
                }
                else if (Player != null)
                {
                    goalCell = new Vec3i(
                        (int)MathF.Floor(Player.GlobalPosition.X),
                        (int)MathF.Round(Player.GlobalPosition.Y),
                        (int)MathF.Floor(Player.GlobalPosition.Z));
                }
                else
                {
                    Log("Coordinates required if player target is not available.");
                    break;
                }

                if (NavigateNpc(target.Id, goalCell, out var result))
                {
                    Log($"Navigating '{target.Name}' to {goalCell}. Waypoints: {result.Waypoints.Count} ({result.Status}, cost: {result.TotalCost:F1})");
                }
                else
                {
                    Log($"Failed to find path for '{target.Name}' to {goalCell}: {result.FailureReason}");
                }
                break;
            }

            case "mine_npc":
            {
                if (parts.Length < 2)
                {
                    Log("Usage: mine_npc <id|name> [x y z]");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                Vec3i targetBlock;
                if (parts.Length >= 5 &&
                    int.TryParse(parts[2], out int mx) &&
                    int.TryParse(parts[3], out int my) &&
                    int.TryParse(parts[4], out int mz))
                {
                    targetBlock = new Vec3i(mx, my, mz);
                }
                else if (Player != null && Player.CurrentRaycast.IsHit)
                {
                    targetBlock = Player.CurrentRaycast.BlockPos;
                }
                else
                {
                    var facing = target.GetFacingDirection();
                    targetBlock = (target.Position + facing * 1.5f).FloorToInt();
                }

                var blocks = NpcManager.BlockRegistry ?? (VoxelWorld as VoxelWorld)?.BlockRegistry;
                if (blocks == null)
                {
                    Log("No block registry available.");
                    break;
                }

                var mineAction = new MineAction(target, targetBlock, VoxelWorld, blocks, NpcManager.ContainerManager);
                mineAction.Start();
                target.CurrentAction = mineAction;
                Log($"Started MineAction on '{target.Name}' targeting {targetBlock}. Status: {mineAction.Status}");
                break;
            }

            case "walk_and_mine":
            {
                if (parts.Length < 2)
                {
                    Log("Usage: walk_and_mine <id|name> [x y z]");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                Vec3i targetBlock;
                if (parts.Length >= 5 &&
                    int.TryParse(parts[2], out int mx) &&
                    int.TryParse(parts[3], out int my) &&
                    int.TryParse(parts[4], out int mz))
                {
                    targetBlock = new Vec3i(mx, my, mz);
                }
                else if (Player != null && Player.CurrentRaycast.IsHit)
                {
                    targetBlock = Player.CurrentRaycast.BlockPos;
                }
                else
                {
                    var facing = target.GetFacingDirection();
                    targetBlock = (target.Position + (facing * 4.0f)).FloorToInt();
                }

                var blocks = NpcManager.BlockRegistry ?? (VoxelWorld as VoxelWorld)?.BlockRegistry;
                if (blocks == null)
                {
                    Log("No block registry available.");
                    break;
                }

                var action = new WalkAndMineAction(
                    target,
                    targetBlock,
                    VoxelWorld,
                    blocks,
                    NpcManager.ContainerManager,
                    PathfindingService,
                    NpcManager.EventBus);

                action.Start();
                target.CurrentAction = action;
                Log($"Started WalkAndMineAction on '{target.Name}' to mine {targetBlock}. Status: {action.Status}");
                break;
            }

            case "place_npc":
            {
                if (parts.Length < 3)
                {
                    Log("Usage: place_npc <id|name> <block_id> [x y z]");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                string blockId = parts[2];
                Vec3i targetBlock;
                if (parts.Length >= 6 &&
                    int.TryParse(parts[3], out int px) &&
                    int.TryParse(parts[4], out int py) &&
                    int.TryParse(parts[5], out int pz))
                {
                    targetBlock = new Vec3i(px, py, pz);
                }
                else if (Player != null && Player.CurrentRaycast.IsHit)
                {
                    targetBlock = Player.CurrentRaycast.AdjacentPos;
                }
                else
                {
                    var facing = target.GetFacingDirection();
                    targetBlock = (target.Position + facing * 1.5f).FloorToInt();
                }

                var blocks = NpcManager.BlockRegistry ?? (VoxelWorld as VoxelWorld)?.BlockRegistry;
                if (blocks == null)
                {
                    Log("No block registry available.");
                    break;
                }

                // Ensure NPC has at least one of the item to place
                if (target.Inventory.CountItem(blockId) == 0)
                {
                    target.Inventory.AddItem(blockId, 5, out _);
                }

                var placeAction = new PlaceAction(target, targetBlock, blockId, VoxelWorld, blocks, NpcManager.ContainerManager, NpcManager.Registry);
                placeAction.Start();
                target.CurrentAction = placeAction;
                Log($"Started PlaceAction on '{target.Name}' placing '{blockId}' at {targetBlock}. Status: {placeAction.Status}");
                break;
            }

            case "say_npc":
            {
                if (parts.Length < 3)
                {
                    Log("Usage: say_npc <id|name> <text>");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                string speechText = string.Join(' ', parts.Skip(2));
                var sayAction = new SayAction(target, speechText, NpcManager.EventBus);
                sayAction.Start();
                Log($"NPC '{target.Name}' said: \"{speechText}\"");
                break;
            }

            case "wait_npc":
            {
                if (parts.Length < 3 || !float.TryParse(parts[2], out float secs))
                {
                    Log("Usage: wait_npc <id|name> <seconds>");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                var waitAction = new WaitAction(target, secs);
                waitAction.Start();
                target.CurrentAction = waitAction;
                Log($"Started WaitAction on '{target.Name}' for {secs}s.");
                break;
            }

            case "cancel_npc":
            {
                if (parts.Length < 2)
                {
                    Log("Usage: cancel_npc <id|name>");
                    break;
                }

                var target = ResolveNpc(parts[1]);
                if (target == null)
                {
                    Log($"NPC '{parts[1]}' not found.");
                    break;
                }

                if (target.CurrentAction != null && target.CurrentAction.Status.IsRunning)
                {
                    target.CurrentAction.Cancel();
                    Log($"Cancelled active action '{target.CurrentAction.Name}' on '{target.Name}'. Status: {target.CurrentAction.Status}");
                    target.CurrentAction = null;
                }
                else
                {
                    Log($"NPC '{target.Name}' has no active running action.");
                }
                break;
            }

            default:
                Log($"Unknown command '{cmd}'. Type 'help' for available commands.");
                break;
        }
    }

    private NpcState? ResolveNpc(string identifier)
    {
        if (int.TryParse(identifier, out int idVal))
        {
            if (NpcManager.Registry.TryGet<NpcState>(new EntityId(idVal), out var npc))
            {
                return npc;
            }
        }

        return NpcManager.ActiveNpcs.FirstOrDefault(n => n.Name.Equals(identifier, StringComparison.OrdinalIgnoreCase));
    }

    private NpcState? FindNearestNpc()
    {
        Vec3 origin = Player != null
            ? new Vec3(Player.GlobalPosition.X, Player.GlobalPosition.Y, Player.GlobalPosition.Z)
            : Vec3.Zero;

        return NpcManager.Registry.FindNearest(origin, 100f, EntityKind.Npc) as NpcState;
    }

    private void BuildUI()
    {
        _panel = new PanelContainer
        {
            Name = "CommandPanel",
            CustomMinimumSize = new Vector2(500, 240)
        };
        AddChild(_panel);

        // Position panel at top-left
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _panel.Position = new Vector2(20, 20);

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.85f),
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        _panel.AddThemeStyleboxOverride("panel", styleBox);

        var vbox = new VBoxContainer();
        _panel.AddChild(vbox);

        var header = new Label
        {
            Text = "Debug Command Console (F1 to toggle, F4=Spawn, F5=Move, F6=Jump, F7=Stop)",
            Modulate = new Color(0.6f, 0.8f, 1f)
        };
        vbox.AddChild(header);

        _output = new RichTextLabel
        {
            CustomMinimumSize = new Vector2(480, 160),
            ScrollFollowing = true,
            BbcodeEnabled = true
        };
        vbox.AddChild(_output);

        _input = new LineEdit
        {
            PlaceholderText = "Enter command (e.g. spawn_npc Bob, move_npc Bob 3 0, jump_npc Bob, help)...",
            CustomMinimumSize = new Vector2(480, 32)
        };
        _input.TextSubmitted += text =>
        {
            Log($"> {text}");
            ExecuteCommand(text);
            _input.Clear();
        };
        vbox.AddChild(_input);
    }
}
