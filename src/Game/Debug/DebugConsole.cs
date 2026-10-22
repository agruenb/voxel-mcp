namespace VoxelKing.Game.Debug;

using System;
using System.Linq;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
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
            npc.Velocity = new Vec3(0f, npc.Velocity.Y, 0f);
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
                Log("  list_npcs                 - List all active NPCs");
                Log("Hotkeys: F4=Spawn, F5=Move, F6=Jump, F7=Stop");
                break;

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
                    Log($"  ID {npc.Id}: '{npc.Name}' at {npc.Position} (Grounded={npc.IsOnGround}, Wall={npc.IsOnWall}, LOD={npc.LodTier})");
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
