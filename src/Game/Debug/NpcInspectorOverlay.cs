namespace VoxelKing.Game.Debug;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Perception;
using VoxelKing.Game.Player;

/// <summary>
/// NPC Inspector Overlay toggled with F3 (F-502, ARCHITECTURE §5.8).
/// Provides a comprehensive, real-time live inspection overlay for NPCs:
/// - Active Arbiter layer, Action, Navigation Path with 3D line visualization
/// - Inventory slots and contents
/// - Memory KV store, named places, and recent memory events ring buffer
/// - Live brain console log output from log(...) calls
/// - Instruction budget usage, forced yield counts, and memory footprint
/// - Last error message and diagnostic alerts
/// - Last prompt and raw response exchanged with the LLM
/// - NPC and global LLM queue token consumption totals
/// </summary>
public partial class NpcInspectorOverlay : CanvasLayer
{
    private PanelContainer _rootPanel = null!;
    private Label _titleLabel = null!;
    private Label _modeBadge = null!;
    private Button _aimLookedAtBtn = null!;
    private Button _walkModeBtn = null!;
    private OptionButton _npcDropdown = null!;
    private Button _closeBtn = null!;

    // Summary Card Controls
    private Label _npcNameLabel = null!;
    private Label _npcDetailsLabel = null!;
    private ProgressBar _healthBar = null!;
    private Label _healthLabel = null!;
    private Label _layerPill = null!;
    private Label _convPill = null!;
    private Label _brainPill = null!;
    private Label _taskSummaryLabel = null!;

    // Tab 1: Overview & Action
    private RichTextLabel _layerDetailsLabel = null!;
    private RichTextLabel _actionLabel = null!;
    private RichTextLabel _pathStatusLabel = null!;
    private RichTextLabel _pathWaypointsList = null!;
    private ProgressBar _budgetProgressBar = null!;
    private Label _budgetProgressLabel = null!;
    private Label _budgetStatsLabel = null!;
    private PanelContainer _errorPanel = null!;
    private Label _errorLabel = null!;

    // Tab 2: Inventory & Memory
    private Label _inventorySummaryLabel = null!;
    private RichTextLabel _inventorySlotsList = null!;
    private Label _memorySummaryLabel = null!;
    private RichTextLabel _memoryKvList = null!;
    private RichTextLabel _placesList = null!;

    // Tab 3: Events & Logs
    private RichTextLabel _eventsList = null!;
    private RichTextLabel _brainLogOutput = null!;
    private Button _clearLogsBtn = null!;

    // Tab 4: LLM & Tokens
    private Label _tokenStatsLabel = null!;
    private RichTextLabel _lastPromptView = null!;
    private RichTextLabel _lastResponseView = null!;

    // Floating Notification Label
    private Label _toastLabel = null!;
    private double _toastTimer = 0.0;

    private readonly List<NpcState> _indexedNpcs = new();
    private NpcState? _targetNpc;
    private bool _isOpen = false;
    private bool _isWalkMode = false;
    private double _refreshTimer = 0.0;
    private const double RefreshIntervalSeconds = 0.1; // 10 Hz live update

    public NpcManager? NpcManager { get; set; }
    public PlayerController? Player { get; set; }
    public IBrainCompiler? BrainCompiler { get; set; }
    public ILlmRequestQueue? LlmQueue { get; set; }
    public DebugPathRenderer? PathRenderer { get; set; }
    public IWorldTime? WorldTime { get; set; }

    public bool IsOpen => _isOpen;
    public bool IsWalkMode => _isWalkMode;
    public NpcState? TargetNpc => _targetNpc;
    public OptionButton NpcDropdown => _npcDropdown;

    public override void _Ready()
    {
        Layer = 102; // Positioned above standard HUD (15), below DebugBrainConsole (105)
        BuildUI();
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        // Toggle Overlay with F3
        if (keyEvent.Keycode == Key.F3)
        {
            ToggleOverlay();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isOpen)
        {
            return;
        }

        // Close on Escape
        if (keyEvent.Keycode == Key.Escape)
        {
            CloseOverlay();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Toggle Walk Mode with 'P' when overlay is open
        if (keyEvent.Keycode == Key.P && !keyEvent.AltPressed && !keyEvent.CtrlPressed)
        {
            ToggleWalkMode();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        // Update toast notification fade
        if (_toastTimer > 0.0)
        {
            _toastTimer -= delta;
            if (_toastTimer <= 0.0 && _toastLabel != null)
            {
                _toastLabel.Visible = false;
            }
        }

        if (!_isOpen)
        {
            return;
        }

        _refreshTimer += delta;
        if (_refreshTimer >= RefreshIntervalSeconds)
        {
            _refreshTimer = 0.0;
            UpdateLiveFields();
        }
    }

    /// <summary>
    /// Toggles the inspector overlay on or off. When opening, targets the looked-at NPC if available.
    /// </summary>
    public void ToggleOverlay()
    {
        if (_isOpen)
        {
            CloseOverlay();
        }
        else
        {
            var target = FindLookedAtNpc(30.0f, 0.75f);
            if (target == null && NpcManager != null && NpcManager.ActiveNpcs.Count > 0)
            {
                // Fallback to nearest NPC within range if not directly under crosshair
                target = FindNearestNpc(35.0f);
            }

            if (target != null)
            {
                OpenOverlay(target);
            }
            else
            {
                ShowToast("No active NPC in line of sight to inspect (Aim at an NPC and press F3).");
            }
        }
    }

    /// <summary>
    /// Opens the inspector overlay targeting the specified NPC.
    /// </summary>
    public void OpenOverlay(NpcState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        _targetNpc = npc;
        _isOpen = true;
        Visible = true;

        if (PathRenderer != null)
        {
            PathRenderer.IsEnabled = true;
        }

        RefreshNpcList();
        SelectNpcInDropdown(npc);

        ApplyWalkMode(_isWalkMode);
        UpdateLiveFields();
    }

    /// <summary>
    /// Closes the inspector overlay and restores player input controls.
    /// </summary>
    public void CloseOverlay()
    {
        _isOpen = false;
        Visible = false;

        if (Player != null)
        {
            Player.IsInputLocked = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>
    /// Toggles Walk Mode, allowing the player to freely walk around with captured mouse while the inspector remains visible.
    /// </summary>
    public void ToggleWalkMode()
    {
        ApplyWalkMode(!_isWalkMode);
    }

    private void ApplyWalkMode(bool walkMode)
    {
        _isWalkMode = walkMode;

        if (_walkModeBtn != null)
        {
            _walkModeBtn.Text = _isWalkMode ? "📌 Walk Mode: ON" : "📌 Walk Mode: OFF";
        }

        if (_modeBadge != null)
        {
            _modeBadge.Text = _isWalkMode ? "[WALK MODE]" : "[INTERACTIVE]";
            _modeBadge.Modulate = _isWalkMode ? new Color(0.4f, 1.0f, 0.5f) : new Color(0.4f, 0.8f, 1.0f);
        }

        if (_isWalkMode)
        {
            if (Player != null)
            {
                Player.IsInputLocked = false;
            }
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            if (Player != null)
            {
                Player.IsInputLocked = true;
            }
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    /// <summary>
    /// Selects a new target NPC to inspect and immediately updates all fields.
    /// </summary>
    public void SelectTargetNpc(NpcState npc)
    {
        _targetNpc = npc;
        SelectNpcInDropdown(npc);
        UpdateLiveFields();
    }

    /// <summary>
    /// Finds the active NPC closest to the player's crosshair view vector within the given max distance.
    /// </summary>
    public NpcState? FindLookedAtNpc(float maxDistance = 30f, float minDotProduct = 0.75f)
    {
        if (NpcManager == null)
        {
            return null;
        }

        Camera3D? cam = null;
        if (Player != null)
        {
            cam = Player.Camera ?? Player.GetNodeOrNull<Camera3D>("Head/Camera3D") ?? Player.GetNodeOrNull<Camera3D>("Camera3D");
        }
        cam ??= GetViewport().GetCamera3D();

        if (cam == null)
        {
            return NpcManager.ActiveNpcs.FirstOrDefault();
        }

        Vector3 camPos = cam.GlobalPosition;
        Vector3 camFwd = -cam.GlobalTransform.Basis.Z.Normalized();

        NpcState? bestNpc = null;
        float bestDot = minDotProduct;

        foreach (var npc in NpcManager.ActiveNpcs)
        {
            if (!npc.IsActive)
            {
                continue;
            }

            Vector3 npcPos = new Vector3(npc.Position.X, npc.Position.Y + 0.9f, npc.Position.Z);
            Vector3 toNpc = npcPos - camPos;
            float dist = toNpc.Length();

            if (dist > maxDistance || dist < 0.2f)
            {
                continue;
            }

            float dot = camFwd.Dot(toNpc / dist);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestNpc = npc;
            }
        }

        return bestNpc;
    }

    /// <summary>
    /// Finds the nearest active NPC to the player within the specified maximum distance.
    /// </summary>
    public NpcState? FindNearestNpc(float maxDistance = 35f)
    {
        if (NpcManager == null)
        {
            return null;
        }

        Vector3 origin = Player?.GlobalPosition ?? Vector3.Zero;
        NpcState? nearest = null;
        float nearestDistSq = maxDistance * maxDistance;

        foreach (var npc in NpcManager.ActiveNpcs)
        {
            if (!npc.IsActive) continue;

            float distSq = origin.DistanceSquaredTo(new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z));
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = npc;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Displays a temporary toast notification message on screen.
    /// </summary>
    public void ShowToast(string message, double durationSeconds = 3.0)
    {
        if (_toastLabel == null) return;

        _toastLabel.Text = message;
        _toastLabel.Visible = true;
        _toastTimer = durationSeconds;
    }

    private void RefreshNpcList()
    {
        if (NpcManager == null || _npcDropdown == null)
        {
            return;
        }

        _indexedNpcs.Clear();
        _npcDropdown.Clear();

        foreach (var npc in NpcManager.ActiveNpcs)
        {
            _indexedNpcs.Add(npc);
            _npcDropdown.AddItem($"#{npc.Id} - {npc.Name}");
        }
    }

    private void SelectNpcInDropdown(NpcState npc)
    {
        if (_npcDropdown == null) return;

        for (int i = 0; i < _indexedNpcs.Count; i++)
        {
            if (_indexedNpcs[i].Id == npc.Id)
            {
                _npcDropdown.Selected = i;
                break;
            }
        }
    }

    /// <summary>
    /// Updates all inspector fields live from the current target NPC state.
    /// </summary>
    public void UpdateLiveFields()
    {
        if (_targetNpc == null)
        {
            return;
        }

        // Verify NPC is still valid and active
        if (NpcManager != null && !NpcManager.ActiveNpcs.Contains(_targetNpc))
        {
            _npcDetailsLabel.Text = "[NPC Inactive / Despawned]";
            return;
        }

        var npc = _targetNpc;

        // 1. Header & Summary Card
        UpdateSummaryCard(npc);

        // 2. Tab 1: Overview & Action
        UpdateOverviewTab(npc);

        // 3. Tab 2: Inventory & Memory
        UpdateInventoryAndMemoryTab(npc);

        // 4. Tab 3: Events & Brain Log
        UpdateEventsAndLogTab(npc);

        // 5. Tab 4: LLM & Token Stats
        UpdateLlmAndTokensTab(npc);
    }

    private void UpdateSummaryCard(NpcState npc)
    {
        // Name & Identity
        _npcNameLabel.Text = $"{npc.Name} (ID: {npc.Id})";
        if (!string.IsNullOrEmpty(npc.AppearanceColor))
        {
            try
            {
                _npcNameLabel.Modulate = Color.FromHtml(npc.AppearanceColor);
            }
            catch
            {
                _npcNameLabel.Modulate = new Color(0.4f, 0.85f, 1.0f);
            }
        }
        else
        {
            _npcNameLabel.Modulate = new Color(0.4f, 0.85f, 1.0f);
        }

        float dist = Player != null
            ? Player.GlobalPosition.DistanceTo(new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z))
            : 0f;

        string skillsText = npc.Skills.Count > 0 ? string.Join(", ", npc.Skills) : "general";
        string personalityText = !string.IsNullOrWhiteSpace(npc.Personality) ? npc.Personality : (!string.IsNullOrWhiteSpace(npc.Profile) ? npc.Profile : "neutral");
        string timeInfo = WorldTime != null ? $" | Time: {WorldTime.TimeOfDayHours:F1}h ({(WorldTime.IsDay ? "Day" : "Night")})" : string.Empty;

        _npcDetailsLabel.Text = $"Pos: ({npc.Position.X:F1}, {npc.Position.Y:F1}, {npc.Position.Z:F1}) | Dist: {dist:F1}m{timeInfo}\nProfile: {personalityText} | Skills: [{skillsText}]";

        // Health Bar
        _healthBar.MaxValue = npc.MaxHealth;
        _healthBar.Value = npc.Health;
        _healthLabel.Text = $"HP: {npc.Health:F0} / {npc.MaxHealth:F0}";

        // Status Pills
        var activeLayer = npc.Arbiter?.ActiveLayer.ToString() ?? "Idle";
        _layerPill.Text = $"Layer: {activeLayer}";
        _layerPill.Modulate = activeLayer switch
        {
            "Reflex" => new Color(1.0f, 0.4f, 0.4f),
            "Task" => new Color(0.35f, 0.95f, 0.45f),
            _ => new Color(0.4f, 0.75f, 1.0f)
        };

        var convState = npc.ConversationState.ToString();
        _convPill.Text = $"Conv: {convState}";
        _convPill.Modulate = convState switch
        {
            "Listening" => new Color(1.0f, 0.85f, 0.2f),
            "Thinking" => new Color(0.85f, 0.45f, 1.0f),
            "Replying" => new Color(0.35f, 0.95f, 0.6f),
            _ => new Color(0.6f, 0.65f, 0.75f)
        };

        var brainStatus = npc.BrainHost?.Status.ToString() ?? "Uninitialized";
        _brainPill.Text = $"Brain: {brainStatus}";
        _brainPill.Modulate = brainStatus switch
        {
            "Running" => new Color(0.35f, 0.95f, 0.45f),
            "Faulted" => new Color(1.0f, 0.35f, 0.35f),
            "Completed" => new Color(0.5f, 0.8f, 1.0f),
            _ => new Color(0.6f, 0.65f, 0.75f)
        };

        // Current Task Summary
        string taskSummary = !string.IsNullOrWhiteSpace(npc.CurrentTaskSummary)
            ? npc.CurrentTaskSummary
            : "(Idle / No active task)";
        _taskSummaryLabel.Text = $"🎯 Task: {taskSummary}";
    }

    private void UpdateOverviewTab(NpcState npc)
    {
        // 1. Layer & Arbiter Details
        string layerName = npc.Arbiter?.ActiveLayer.ToString() ?? "None";
        string lod = npc.LodTier.ToString();
        _layerDetailsLabel.Text = $"Active Arbiter Layer: [b]{layerName}[/b]  |  LOD Tier: [b]{lod}[/b]\nBrain Ticks: {npc.BrainTickCount:N0}  |  Last Tick Delta: {npc.TimeSinceLastBrainTick:F3}s";

        // 2. Action
        var action = npc.CurrentAction;
        if (action != null)
        {
            string failReason = action.Status.FailureReason != null ? $" (Reason: {action.Status.FailureReason})" : "";
            _actionLabel.Text = $"Action: [color=cyan]{action.Name}[/color]  |  Status: [b]{action.Status}[/b]{failReason}";
        }
        else
        {
            _actionLabel.Text = "Action: [color=gray](Idle / None)[/color]";
        }

        // 3. Navigation Path
        var path = npc.ActivePath;
        if (path != null && path.Count > 0)
        {
            var nextWp = path[0];
            var destWp = path[^1];
            _pathStatusLabel.Text = $"Path: [color=lime]Navigating[/color] ({path.Count} waypoints remaining)  |  Target: ({destWp.X}, {destWp.Y}, {destWp.Z})";

            var waypointsStr = string.Join("  ➜  ", path.Select((wp, idx) => $"[{idx + 1}] ({wp.X},{wp.Y},{wp.Z})"));
            _pathWaypointsList.Text = waypointsStr;
        }
        else
        {
            _pathStatusLabel.Text = "Path: [color=gray]No active navigation path[/color]";
            _pathWaypointsList.Text = "[color=gray]None[/color]";
        }

        // 4. Instruction Budget Usage
        var host = npc.BrainHost;
        if (host != null)
        {
            int maxBudget = host.MaxInstructionsPerTick;
            int lastUsed = host.LastTickInstructions;
            float pct = maxBudget > 0 ? (float)lastUsed / maxBudget * 100f : 0f;

            _budgetProgressBar.MaxValue = maxBudget;
            _budgetProgressBar.Value = lastUsed;
            _budgetProgressLabel.Text = $"{lastUsed:N0} / {maxBudget:N0} instructions ({pct:F1}%)";

            int memBytes = npc.Memory.TotalSizeBytes;
            int memKeys = npc.Memory.KeyCount;
            _budgetStatsLabel.Text = $"Total Instructions: {host.TotalInstructionsExecuted:N0}\nConsecutive Forced Yields: {host.ConsecutiveForcedYields} / {host.MaxConsecutiveForcedYields}\nMemory Footprint: {memBytes:N0} / 16,384 bytes  |  Keys: {memKeys} / 64";
        }
        else
        {
            _budgetProgressBar.Value = 0;
            _budgetProgressLabel.Text = "No BrainHost";
            _budgetStatsLabel.Text = "Memory Footprint: " + npc.Memory.TotalSizeBytes + " bytes";
        }

        // 5. Last Error
        string? lastErr = host?.LastError;
        if (!string.IsNullOrEmpty(lastErr))
        {
            _errorPanel.Visible = true;
            _errorLabel.Text = $"⚠️ Brain Error: {lastErr}";
        }
        else
        {
            _errorPanel.Visible = false;
        }
    }

    private void UpdateInventoryAndMemoryTab(NpcState npc)
    {
        // 1. Inventory
        var inv = npc.Inventory;
        if (inv != null)
        {
            int usedSlots = inv.SlotCount - inv.FreeSlots;
            _inventorySummaryLabel.Text = $"Inventory: {usedSlots} / {inv.SlotCount} slots used ({inv.FreeSlots} free)";

            var items = new List<string>();
            for (int i = 0; i < inv.SlotCount; i++)
            {
                var stack = inv.GetSlot(i);
                if (stack != null && stack.Count > 0)
                {
                    items.Add($"Slot {i:D2}: [color=gold]{stack.ItemId}[/color] × {stack.Count}");
                }
            }

            _inventorySlotsList.Text = items.Count > 0
                ? string.Join("\n", items)
                : "[color=gray](Inventory is empty)[/color]";
        }
        else
        {
            _inventorySummaryLabel.Text = "Inventory: (None)";
            _inventorySlotsList.Text = "[color=gray](No inventory)[/color]";
        }

        // 2. Memory KV Store
        var kv = npc.Memory.KeyValues;
        _memorySummaryLabel.Text = $"KV Store: {kv.Count} keys ({npc.Memory.TotalSizeBytes} / 16,384 B)";

        if (kv.Count > 0)
        {
            var kvLines = kv.Select(pair =>
            {
                string valStr = pair.Value is string s ? $"\"{s}\"" : pair.Value?.ToString() ?? "null";
                return $"• [color=cyan]{pair.Key}[/color] = {valStr}";
            });
            _memoryKvList.Text = string.Join("\n", kvLines);
        }
        else
        {
            _memoryKvList.Text = "[color=gray](No keys stored in memory)[/color]";
        }

        // 3. Known Places
        var places = npc.Memory.KnownPlaces;
        if (places.Count > 0)
        {
            var placeLines = places.Select(p => $"🏠 [color=lime]{p.Key}[/color]: ({p.Value.X}, {p.Value.Y}, {p.Value.Z})");
            _placesList.Text = string.Join("\n", placeLines);
        }
        else
        {
            _placesList.Text = "[color=gray](No remembered places)[/color]";
        }
    }

    private void UpdateEventsAndLogTab(NpcState npc)
    {
        // 1. Recent Memory Events
        var events = npc.Memory.GetRecentEvents(25);
        if (events.Count > 0)
        {
            var eventLines = events.Select(evt =>
            {
                string tag = evt.EventType switch
                {
                    "instruction" => "[color=cyan][Instruction][/color]",
                    "heard" => "[color=yellow][Heard][/color]",
                    "spoke" => "[color=lime][Spoke][/color]",
                    "attacked" => "[color=salmon][Attacked][/color]",
                    "brain_error" => "[color=red][Error][/color]",
                    "task_finished" => "[color=gold][Done][/color]",
                    _ => $"[color=gray][{evt.EventType}][/color]"
                };
                return $"[{evt.Timestamp:F1}s] {tag} {evt.RenderContent()}";
            });

            _eventsList.Text = string.Join("\n", eventLines);
        }
        else
        {
            _eventsList.Text = "[color=gray](No recent memory events recorded)[/color]";
        }

        // 2. Brain Log Output
        var host = npc.BrainHost;
        if (host != null)
        {
            var logs = host.RecentLogs;
            _brainLogOutput.Text = logs.Count > 0
                ? string.Join("\n", logs)
                : "[color=gray](No brain logs emitted yet. Use log(...) in Lua script)[/color]";
        }
        else
        {
            _brainLogOutput.Text = "[color=gray](No brain host)[/color]";
        }
    }

    private void UpdateLlmAndTokensTab(NpcState npc)
    {
        // 1. Token Usage Totals
        var npcTokens = npc.TotalTokenUsage;
        var queueStats = LlmQueue?.UsageStats;

        string globalStatsStr = queueStats != null
            ? $"Global Queue Totals: Prompt: {queueStats.TotalPromptTokens:N0}  |  Output: {queueStats.TotalOutputTokens:N0}  |  Total: {queueStats.TotalTokens:N0}\nRequests: {queueStats.TotalRequests:N0} Total  |  {queueStats.SuccessfulRequests:N0} Succeeded  |  {queueStats.FailedRequests:N0} Failed  |  {queueStats.InFlightRequests} In-Flight"
            : "Global Queue: (Not available)";

        _tokenStatsLabel.Text = $"NPC Token Usage: Prompt: [b]{npcTokens.PromptTokens:N0}[/b]  |  Output: [b]{npcTokens.OutputTokens:N0}[/b]  |  Total: [b]{npcTokens.TotalTokens:N0}[/b]\n{globalStatsStr}";

        // 2. Last Prompt
        string prompt = !string.IsNullOrWhiteSpace(npc.LastPrompt)
            ? npc.LastPrompt
            : "(No LLM instruction prompt sent yet for this NPC)";
        _lastPromptView.Text = prompt;

        // 3. Last Response
        string response = !string.IsNullOrWhiteSpace(npc.LastResponse)
            ? npc.LastResponse
            : "(No LLM response received yet for this NPC)";
        _lastResponseView.Text = response;
    }

    private void BuildUI()
    {
        // 1. Toast Notification Label at top-center of screen
        _toastLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false
        };
        _toastLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _toastLabel.OffsetTop = 24;
        _toastLabel.OffsetBottom = 60;

        var toastStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.12f, 0.18f, 0.92f),
            BorderColor = new Color(0.3f, 0.7f, 1.0f, 0.8f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 20,
            ContentMarginRight = 20
        };
        _toastLabel.AddThemeStyleboxOverride("normal", toastStyle);
        AddChild(_toastLabel);

        // 2. Main Root Panel (Anchored to right side)
        _rootPanel = new PanelContainer
        {
            Name = "InspectorRootPanel"
        };
        _rootPanel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        _rootPanel.OffsetLeft = -520;
        _rootPanel.OffsetTop = 16;
        _rootPanel.OffsetRight = -16;
        _rootPanel.OffsetBottom = -16;

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.12f, 0.94f),
            BorderColor = new Color(0.25f, 0.50f, 0.85f, 0.70f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 16,
            ContentMarginTop = 14,
            ContentMarginRight = 16,
            ContentMarginBottom = 14
        };
        _rootPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_rootPanel);

        var mainVBox = new VBoxContainer
        {
            OffsetLeft = 0,
            OffsetTop = 0
        };
        mainVBox.AddThemeConstantOverride("separation", 8);
        _rootPanel.AddChild(mainVBox);

        // --- Top Header Bar ---
        var headerHBox = new HBoxContainer();
        headerHBox.AddThemeConstantOverride("separation", 8);
        mainVBox.AddChild(headerHBox);

        _titleLabel = new Label
        {
            Text = "🔍 NPC INSPECTOR [F3]",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.85f, 1.0f));
        headerHBox.AddChild(_titleLabel);

        _modeBadge = new Label
        {
            Text = "[INTERACTIVE]"
        };
        _modeBadge.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1.0f));
        headerHBox.AddChild(_modeBadge);

        _walkModeBtn = new Button
        {
            Text = "📌 Walk Mode: OFF",
            TooltipText = "Toggle Walk Mode (P): Keeps inspector visible while restoring mouse control to walk around"
        };
        _walkModeBtn.Pressed += ToggleWalkMode;
        headerHBox.AddChild(_walkModeBtn);

        _closeBtn = new Button
        {
            Text = "✕",
            TooltipText = "Close Inspector (F3 or Esc)"
        };
        _closeBtn.Pressed += CloseOverlay;
        headerHBox.AddChild(_closeBtn);

        // --- NPC Selection Bar ---
        var selectHBox = new HBoxContainer();
        selectHBox.AddThemeConstantOverride("separation", 8);
        mainVBox.AddChild(selectHBox);

        var selectLabel = new Label { Text = "Target NPC:" };
        selectHBox.AddChild(selectLabel);

        _npcDropdown = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _npcDropdown.ItemSelected += index =>
        {
            if (index >= 0 && index < _indexedNpcs.Count)
            {
                SelectTargetNpc(_indexedNpcs[(int)index]);
            }
        };
        selectHBox.AddChild(_npcDropdown);

        _aimLookedAtBtn = new Button
        {
            Text = "🎯 Looked-At",
            TooltipText = "Inspect NPC currently under player crosshair"
        };
        _aimLookedAtBtn.Pressed += () =>
        {
            var lookedAt = FindLookedAtNpc();
            if (lookedAt != null)
            {
                SelectTargetNpc(lookedAt);
            }
            else
            {
                ShowToast("No NPC found in line of sight.");
            }
        };
        selectHBox.AddChild(_aimLookedAtBtn);

        // --- Summary Card ---
        var summaryPanel = new PanelContainer();
        var summaryStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.12f, 0.18f, 0.85f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8
        };
        summaryPanel.AddThemeStyleboxOverride("panel", summaryStyle);
        mainVBox.AddChild(summaryPanel);

        var summaryVBox = new VBoxContainer();
        summaryVBox.AddThemeConstantOverride("separation", 4);
        summaryPanel.AddChild(summaryVBox);

        var nameHBox = new HBoxContainer();
        nameHBox.AddThemeConstantOverride("separation", 8);
        summaryVBox.AddChild(nameHBox);

        _npcNameLabel = new Label
        {
            Text = "NPC Name",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        nameHBox.AddChild(_npcNameLabel);

        _healthLabel = new Label { Text = "HP: 100/100" };
        nameHBox.AddChild(_healthLabel);

        _healthBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 6)
        };
        summaryVBox.AddChild(_healthBar);

        _npcDetailsLabel = new Label
        {
            Text = "Pos: (0, 0, 0) | Dist: 0m",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _npcDetailsLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.80f, 0.90f));
        summaryVBox.AddChild(_npcDetailsLabel);

        var pillsHBox = new HBoxContainer();
        pillsHBox.AddThemeConstantOverride("separation", 6);
        summaryVBox.AddChild(pillsHBox);

        _layerPill = CreateStatusPill("Layer: Task", new Color(0.35f, 0.95f, 0.45f));
        pillsHBox.AddChild(_layerPill);

        _convPill = CreateStatusPill("Conv: None", new Color(0.6f, 0.65f, 0.75f));
        pillsHBox.AddChild(_convPill);

        _brainPill = CreateStatusPill("Brain: Running", new Color(0.35f, 0.95f, 0.45f));
        pillsHBox.AddChild(_brainPill);

        _taskSummaryLabel = new Label
        {
            Text = "🎯 Task: None (Idle)",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _taskSummaryLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.4f));
        summaryVBox.AddChild(_taskSummaryLabel);

        // --- Tabs Section ---
        var tabs = new TabContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 360)
        };
        mainVBox.AddChild(tabs);

        // TAB 1: Overview & Action
        tabs.AddChild(BuildOverviewTab());
        tabs.SetTabTitle(0, "📊 Overview");

        // TAB 2: Inventory & Memory
        tabs.AddChild(BuildInventoryMemoryTab());
        tabs.SetTabTitle(1, "🎒 Inventory & Memory");

        // TAB 3: Events & Logs
        tabs.AddChild(BuildEventsLogsTab());
        tabs.SetTabTitle(2, "📜 Events & Log");

        // TAB 4: LLM & Tokens
        tabs.AddChild(BuildLlmTokensTab());
        tabs.SetTabTitle(3, "🤖 LLM & Tokens");
    }

    private Control BuildOverviewTab()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        // Section: Layer & Arbiter
        var layerHeader = CreateSectionHeader("ARBITER & SIMULATION");
        vbox.AddChild(layerHeader);

        _layerDetailsLabel = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "Active Arbiter Layer: Task",
            CustomMinimumSize = new Vector2(0, 32)
        };
        vbox.AddChild(_layerDetailsLabel);

        // Section: Action
        var actionHeader = CreateSectionHeader("CURRENT ACTION");
        vbox.AddChild(actionHeader);

        _actionLabel = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "Action: Idle",
            CustomMinimumSize = new Vector2(0, 24)
        };
        vbox.AddChild(_actionLabel);

        // Section: Path & Navigation
        var pathHeader = CreateSectionHeader("NAVIGATION PATH");
        vbox.AddChild(pathHeader);

        _pathStatusLabel = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "Path: No active navigation path",
            CustomMinimumSize = new Vector2(0, 24)
        };
        vbox.AddChild(_pathStatusLabel);

        _pathWaypointsList = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "None",
            CustomMinimumSize = new Vector2(0, 40)
        };
        vbox.AddChild(_pathWaypointsList);

        // Section: Instruction Budget Usage
        var budgetHeader = CreateSectionHeader("INSTRUCTION BUDGET USAGE");
        vbox.AddChild(budgetHeader);

        var progressHBox = new HBoxContainer();
        progressHBox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(progressHBox);

        _budgetProgressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 2000,
            Value = 0,
            ShowPercentage = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 16)
        };
        progressHBox.AddChild(_budgetProgressBar);

        _budgetProgressLabel = new Label
        {
            Text = "0 / 2,000 instructions (0.0%)"
        };
        progressHBox.AddChild(_budgetProgressLabel);

        _budgetStatsLabel = new Label
        {
            Text = "Total Instructions: 0\nConsecutive Forced Yields: 0 / 50\nMemory Footprint: 0 / 16,384 B"
        };
        _budgetStatsLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.80f, 0.90f));
        vbox.AddChild(_budgetStatsLabel);

        // Error Panel
        _errorPanel = new PanelContainer { Visible = false };
        var errStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.35f, 0.08f, 0.08f, 0.85f),
            BorderColor = new Color(1.0f, 0.35f, 0.35f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6
        };
        _errorPanel.AddThemeStyleboxOverride("panel", errStyle);
        vbox.AddChild(_errorPanel);

        _errorLabel = new Label
        {
            Text = "⚠️ Error: None",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _errorLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.45f));
        _errorPanel.AddChild(_errorLabel);

        return scroll;
    }

    private Control BuildInventoryMemoryTab()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        // Section: Inventory
        var invHeader = CreateSectionHeader("INVENTORY SLOTS");
        vbox.AddChild(invHeader);

        _inventorySummaryLabel = new Label { Text = "Inventory: 0 / 36 slots used" };
        vbox.AddChild(_inventorySummaryLabel);

        _inventorySlotsList = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "(Inventory is empty)",
            CustomMinimumSize = new Vector2(0, 80)
        };
        vbox.AddChild(_inventorySlotsList);

        // Section: Memory KV Store
        var memHeader = CreateSectionHeader("MEMORY KEY-VALUE STORE");
        vbox.AddChild(memHeader);

        _memorySummaryLabel = new Label { Text = "KV Store: 0 keys (0 / 16,384 B)" };
        vbox.AddChild(_memorySummaryLabel);

        _memoryKvList = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "(No keys stored)",
            CustomMinimumSize = new Vector2(0, 80)
        };
        vbox.AddChild(_memoryKvList);

        // Section: Known Places
        var placesHeader = CreateSectionHeader("KNOWN NAMED PLACES");
        vbox.AddChild(placesHeader);

        _placesList = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            Text = "(No places remembered)",
            CustomMinimumSize = new Vector2(0, 40)
        };
        vbox.AddChild(_placesList);

        return scroll;
    }

    private Control BuildEventsLogsTab()
    {
        var vbox = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        vbox.AddThemeConstantOverride("separation", 8);

        // Section: Recent Events Ring Buffer
        var eventsHeader = CreateSectionHeader("RECENT MEMORY EVENTS (RING BUFFER)");
        vbox.AddChild(eventsHeader);

        var eventsScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 140)
        };
        vbox.AddChild(eventsScroll);

        _eventsList = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = "(No recent events)"
        };
        eventsScroll.AddChild(_eventsList);

        // Section: Brain Console Logs
        var logHeaderHBox = new HBoxContainer();
        logHeaderHBox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(logHeaderHBox);

        var logHeader = CreateSectionHeader("BRAIN CONSOLE LOG (log(...) stream)");
        logHeader.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        logHeaderHBox.AddChild(logHeader);

        _clearLogsBtn = new Button
        {
            Text = "Clear Log",
            CustomMinimumSize = new Vector2(70, 24)
        };
        _clearLogsBtn.Pressed += () =>
        {
            _brainLogOutput.Text = "[color=gray](Logs cleared)[/color]";
        };
        logHeaderHBox.AddChild(_clearLogsBtn);

        var logScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 140)
        };
        vbox.AddChild(logScroll);

        _brainLogOutput = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = "(No brain logs emitted yet)"
        };
        logScroll.AddChild(_brainLogOutput);

        return vbox;
    }

    private Control BuildLlmTokensTab()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        // Section: Token Stats
        var tokenHeader = CreateSectionHeader("TOKEN CONSUMPTION TOTALS");
        vbox.AddChild(tokenHeader);

        _tokenStatsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            Text = "This NPC: 0 tokens\nGlobal LLM Queue: 0 tokens"
        };
        vbox.AddChild(_tokenStatsLabel);

        // Section: Last Prompt
        var promptHeader = CreateSectionHeader("LAST LLM PROMPT (SENT TO MODEL)");
        vbox.AddChild(promptHeader);

        _lastPromptView = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = false,
            Text = "(No LLM instruction prompt sent yet)",
            CustomMinimumSize = new Vector2(0, 120)
        };
        vbox.AddChild(_lastPromptView);

        // Section: Last Response
        var respHeader = CreateSectionHeader("LAST LLM RESPONSE (RAW MODEL OUTPUT)");
        vbox.AddChild(respHeader);

        _lastResponseView = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = false,
            Text = "(No LLM response received yet)",
            CustomMinimumSize = new Vector2(0, 120)
        };
        vbox.AddChild(_lastResponseView);

        return scroll;
    }

    private static Label CreateSectionHeader(string title)
    {
        var label = new Label
        {
            Text = title
        };
        label.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 1.0f));
        return label;
    }

    private static Label CreateStatusPill(string text, Color color)
    {
        var pill = new Label
        {
            Text = text,
            Modulate = color
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.16f, 0.22f, 0.8f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        pill.AddThemeStyleboxOverride("normal", style);
        return pill;
    }
}
