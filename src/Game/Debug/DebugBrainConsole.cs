namespace VoxelKing.Game.Debug;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Player;

/// <summary>
/// Debug Brain Console overlay toggled with F2 (F-306).
/// Allows selecting any NPC, loading example brains or pasting Lua, validating scripts,
/// applying them directly to NPC BrainHosts, and viewing live logs and validation errors.
/// </summary>
public partial class DebugBrainConsole : CanvasLayer
{
    private PanelContainer _mainPanel = null!;
    private OptionButton _npcDropdown = null!;
    private Label _npcInfoLabel = null!;
    private OptionButton _exampleDropdown = null!;
    private Label _exampleInfoLabel = null!;
    private CodeEdit _scriptEditor = null!;
    private TabContainer _outputTabs = null!;
    private RichTextLabel _validationOutput = null!;
    private RichTextLabel _logOutput = null!;
    private Button _applyButton = null!;
    private Button _validateButton = null!;
    private Button _restartButton = null!;
    private Button _stopButton = null!;
    private Button _pickLookedAtButton = null!;
    private Button _loadExampleButton = null!;
    private Button _loadCurrentScriptButton = null!;
    private Button _clearLogsButton = null!;
    private Button _closeButton = null!;

    private readonly List<BrainExample> _examples = new();
    private readonly List<NpcState> _indexedNpcs = new();
    private NpcState? _selectedNpc;
    private double _refreshTimer = 0.0;
    private bool _isOpen = false;

    public NpcManager NpcManager { get; set; } = null!;
    public PlayerController? Player { get; set; }
    public ILuaRuntime? LuaRuntime { get; set; }
    public IBrainValidator Validator { get; set; } = null!;

    public bool IsOpen => _isOpen;
    public CodeEdit ScriptEditor => _scriptEditor;
    public RichTextLabel ValidationOutput => _validationOutput;
    public RichTextLabel LogOutput => _logOutput;
    public OptionButton ExampleDropdown => _exampleDropdown;
    public OptionButton NpcDropdown => _npcDropdown;
    public IReadOnlyList<BrainExample> Examples => _examples;
    public NpcState? SelectedNpc => _selectedNpc;

    public override void _Ready()
    {
        Layer = 105; // Above regular HUD and debug overlay
        Validator ??= new BrainValidator(null, LuaRuntime);

        BuildUI();
        LoadExamples();
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        // Toggle Console with F2
        if (keyEvent.Keycode == Key.F2)
        {
            ToggleConsole();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isOpen && keyEvent.Keycode == Key.Escape)
        {
            CloseConsole();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_isOpen || _selectedNpc == null)
        {
            return;
        }

        _refreshTimer += delta;
        if (_refreshTimer >= 0.25)
        {
            _refreshTimer = 0.0;
            UpdateNpcInfoDisplay();
            UpdateLogDisplay();
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

        if (Player != null)
        {
            Player.IsInputLocked = true;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;

        RefreshNpcList();

        // Attempt to auto-select looked-at NPC if available
        var lookedAt = FindLookedAtNpc();
        if (lookedAt != null)
        {
            SelectNpc(lookedAt);
        }
        else if (_selectedNpc == null && _indexedNpcs.Count > 0)
        {
            SelectNpc(_indexedNpcs[0]);
        }
        else if (_selectedNpc != null)
        {
            SelectNpc(_selectedNpc);
        }

        UpdateNpcInfoDisplay();
        UpdateLogDisplay();
    }

    public void CloseConsole()
    {
        _isOpen = false;
        Visible = false;

        if (Player != null)
        {
            Player.IsInputLocked = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void SelectNpc(NpcState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        _selectedNpc = npc;

        int index = _indexedNpcs.IndexOf(npc);
        if (index >= 0 && index < _npcDropdown.ItemCount)
        {
            _npcDropdown.Selected = index;
        }

        UpdateNpcInfoDisplay();
        UpdateLogDisplay();
    }

    public NpcState? FindLookedAtNpc(float maxDistance = 30f, float minDotProduct = 0.82f)
    {
        if (Player == null || NpcManager == null)
        {
            return null;
        }

        var cam = Player.Camera ?? Player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (cam == null)
        {
            return null;
        }

        Vector3 camPos = cam.GlobalPosition;
        Vector3 camFwd = -cam.GlobalTransform.Basis.Z.Normalized();

        NpcState? bestNpc = null;
        float bestDot = minDotProduct;

        foreach (var npc in NpcManager.ActiveNpcs)
        {
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

    public void ApplyCurrentScript()
    {
        if (_selectedNpc == null)
        {
            ShowValidationMessage("[color=salmon][Error][/color] No target NPC selected.", false);
            return;
        }

        if (_selectedNpc.BrainHost == null)
        {
            ShowValidationMessage($"[color=salmon][Error][/color] NPC '{_selectedNpc.Name}' does not have a BrainHost initialized.", false);
            return;
        }

        string scriptText = _scriptEditor.Text;
        if (string.IsNullOrWhiteSpace(scriptText))
        {
            ShowValidationMessage("[color=salmon][Error][/color] Script editor is empty.", false);
            return;
        }

        // 1. Validate script
        var validation = Validator.Validate(scriptText);
        if (!validation.Ok)
        {
            var errLines = validation.Errors.Select(e =>
                $"• [color=salmon]Stage {e.Stage}[/color]" +
                (e.Line.HasValue ? $" (Line {e.Line})" : "") +
                $": {e.Message}");

            ShowValidationMessage(
                $"[b][color=salmon]Validation Failed ({validation.Errors.Count} error(s)):[/color][/b]\n" +
                string.Join("\n", errLines),
                isSuccess: false);
            return;
        }

        // 2. Apply script directly to NPC's BrainHost
        EntityId? instructor = Player != null ? new EntityId(1) : null;
        bool loaded = _selectedNpc.BrainHost.LoadScript(scriptText, instructor);

        if (!loaded)
        {
            ShowValidationMessage(
                $"[color=salmon][Load Failed][/color] {_selectedNpc.BrainHost.LastError}",
                isSuccess: false);
            return;
        }

        ShowValidationMessage(
            $"[b][color=springgreen]Validation Succeeded![/color][/b]\n" +
            $"Script applied directly to NPC '[b]{_selectedNpc.Name}[/b]' (ID {_selectedNpc.Id}).\n" +
            $"Brain status: [color=cyan]{_selectedNpc.BrainHost.Status}[/color]",
            isSuccess: true);

        UpdateNpcInfoDisplay();
        UpdateLogDisplay();
    }

    public void ValidateCurrentScriptOnly()
    {
        string scriptText = _scriptEditor.Text;
        if (string.IsNullOrWhiteSpace(scriptText))
        {
            ShowValidationMessage("[color=salmon][Error][/color] Script editor is empty.", false);
            return;
        }

        var validation = Validator.Validate(scriptText);
        if (validation.Ok)
        {
            ShowValidationMessage(
                "[b][color=springgreen]✓ Script passed all validation checks successfully![/color][/b]\n" +
                "• Stage 2: Size <= 8 KB (Passed)\n" +
                "• Stage 3: Sandbox syntax compilation (Passed)\n" +
                "• Stage 4: Top-level execution and 'main()' definition (Passed)",
                isSuccess: true);
        }
        else
        {
            var errLines = validation.Errors.Select(e =>
                $"• [color=salmon]Stage {e.Stage}[/color]" +
                (e.Line.HasValue ? $" (Line {e.Line})" : "") +
                $": {e.Message}");

            ShowValidationMessage(
                $"[b][color=salmon]✗ Validation Failed ({validation.Errors.Count} error(s)):[/color][/b]\n" +
                string.Join("\n", errLines),
                isSuccess: false);
        }
    }

    public void LoadSelectedExample()
    {
        int selectedIndex = _exampleDropdown.Selected;
        if (selectedIndex < 0 || selectedIndex >= _examples.Count)
        {
            return;
        }

        var example = _examples[selectedIndex];
        _scriptEditor.Text = example.Script;

        _exampleInfoLabel.Text =
            $"Example: {example.FileName}  |  Valid: {example.IsValid}\n" +
            $"Instruction: \"{example.Instruction}\"\n" +
            $"Reply: \"{example.Reply}\"";

        ShowValidationMessage($"[color=lightblue]Loaded example '{example.FileName}' into editor.[/color]", true);
    }

    public void LoadCurrentNpcScript()
    {
        if (_selectedNpc?.BrainHost == null)
        {
            ShowValidationMessage("[color=salmon][Error][/color] No selected NPC with BrainHost.", false);
            return;
        }

        string? current = _selectedNpc.BrainHost.CurrentScript;
        if (string.IsNullOrEmpty(current))
        {
            ShowValidationMessage($"[color=yellow]NPC '{_selectedNpc.Name}' has no assigned script.[/color]", false);
            return;
        }

        _scriptEditor.Text = current;
        _exampleInfoLabel.Text = $"Loaded active script from NPC '{_selectedNpc.Name}' (ID {_selectedNpc.Id})";
        ShowValidationMessage($"[color=lightblue]Loaded current script of NPC '{_selectedNpc.Name}'.[/color]", true);
    }

    public void RestartSelectedBrain()
    {
        if (_selectedNpc?.BrainHost == null)
        {
            return;
        }

        _selectedNpc.BrainHost.Restart();
        ShowValidationMessage($"[color=springgreen]Restarted brain coroutine on NPC '{_selectedNpc.Name}'.[/color]", true);
        UpdateNpcInfoDisplay();
    }

    public void StopSelectedBrain()
    {
        if (_selectedNpc?.BrainHost == null)
        {
            return;
        }

        _selectedNpc.BrainHost.OnInterrupted("manual debug stop");
        ShowValidationMessage($"[color=yellow]Interrupted/stopped brain on NPC '{_selectedNpc.Name}'.[/color]", true);
        UpdateNpcInfoDisplay();
    }

    private void RefreshNpcList()
    {
        _npcDropdown.Clear();
        _indexedNpcs.Clear();

        if (NpcManager == null)
        {
            return;
        }

        var npcs = NpcManager.ActiveNpcs.ToList();
        for (int i = 0; i < npcs.Count; i++)
        {
            var npc = npcs[i];
            _indexedNpcs.Add(npc);
            string status = npc.BrainHost != null ? npc.BrainHost.Status.ToString() : "NoBrain";
            _npcDropdown.AddItem($"[ID {npc.Id.Value}] {npc.Name} ({status}) at ({npc.Position.X:F0},{npc.Position.Y:F0},{npc.Position.Z:F0})");
        }

        if (_indexedNpcs.Count == 0)
        {
            _npcDropdown.AddItem("No NPCs active in world");
            _selectedNpc = null;
        }
    }

    private void LoadExamples()
    {
        _examples.Clear();
        _exampleDropdown.Clear();

        try
        {
            string examplesDir = ProjectSettings.GlobalizePath("res://brains/examples");
            var loaded = BrainExampleLibrary.LoadFromDirectory(examplesDir);
            _examples.AddRange(loaded);

            for (int i = 0; i < _examples.Count; i++)
            {
                var ex = _examples[i];
                string prefix = ex.IsValid ? "✓ " : "✗ (Invalid) ";
                _exampleDropdown.AddItem($"{prefix}{ex.RelativePath}");
            }

            if (_examples.Count > 0)
            {
                _exampleDropdown.Selected = 0;
                LoadSelectedExample();
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[DebugBrainConsole] Failed to load brain examples: {ex.Message}");
            _exampleDropdown.AddItem("Failed to load examples directory");
        }
    }

    private void UpdateNpcInfoDisplay()
    {
        if (_selectedNpc == null)
        {
            _npcInfoLabel.Text = "No NPC selected.\nSpawn an NPC with F4 or select one from the dropdown.";
            return;
        }

        var host = _selectedNpc.BrainHost;
        string statusText = host != null ? host.Status.ToString() : "None";
        string statusColor = statusText switch
        {
            "Running" => "springgreen",
            "Completed" => "yellow",
            "Faulted" => "salmon",
            _ => "gray"
        };

        string currentAction = _selectedNpc.CurrentAction?.Name ?? "Idle";
        string lastError = host?.LastError != null ? $"[color=salmon]{host.LastError}[/color]" : "None";
        string places = string.Join(", ", _selectedNpc.Memory.Places);
        if (string.IsNullOrEmpty(places)) places = "(none)";

        _npcInfoLabel.Text =
            $"Name: [b]{_selectedNpc.Name}[/b]  |  ID: [b]{_selectedNpc.Id.Value}[/b]\n" +
            $"Status: [color={statusColor}][b]{statusText}[/b][/color]  |  Action: [color=cyan]{currentAction}[/color]\n" +
            $"Health: {_selectedNpc.Health:F0} / {_selectedNpc.MaxHealth:F0}  |  Pos: ({_selectedNpc.Position.X:F1}, {_selectedNpc.Position.Y:F1}, {_selectedNpc.Position.Z:F1})\n" +
            $"Places: {places}\n" +
            $"Last Error: {lastError}";
    }

    private void UpdateLogDisplay()
    {
        if (_selectedNpc?.BrainHost == null)
        {
            _logOutput.Text = "[color=gray]No NPC selected or NPC has no BrainHost.[/color]";
            return;
        }

        var logs = _selectedNpc.BrainHost.RecentLogs;
        if (logs.Count == 0)
        {
            _logOutput.Text = "[color=gray]No recent brain log messages.[/color]";
            return;
        }

        var formattedLogs = logs.Select(log =>
        {
            if (log.Contains("ERROR"))
            {
                return $"[color=salmon]{log}[/color]";
            }
            if (log.Contains("said:"))
            {
                return $"[color=gold]{log}[/color]";
            }
            return $"[color=lightgray]{log}[/color]";
        });

        _logOutput.Text = string.Join("\n", formattedLogs);
    }

    private void ShowValidationMessage(string bbcode, bool isSuccess)
    {
        _validationOutput.Text = bbcode;
        _outputTabs.CurrentTab = 0; // Switch to validation tab
    }

    private void BuildUI()
    {
        // Full screen root container to guarantee proper responsive centering across all viewport sizes
        var rootContainer = new CenterContainer
        {
            Name = "BrainConsoleCenterContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rootContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rootContainer);

        _mainPanel = new PanelContainer
        {
            Name = "BrainConsolePanel",
            CustomMinimumSize = new Vector2(880, 580)
        };
        rootContainer.AddChild(_mainPanel);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.14f, 0.96f),
            BorderColor = new Color(0.25f, 0.45f, 0.75f, 0.85f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12
        };
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var outerVBox = new VBoxContainer { Name = "OuterVBox" };
        outerVBox.AddThemeConstantOverride("separation", 10);
        _mainPanel.AddChild(outerVBox);

        // 1. Header Bar
        var headerHBox = new HBoxContainer();
        outerVBox.AddChild(headerHBox);

        var titleLabel = new Label
        {
            Text = "🧠  NPC LUA BRAIN CONSOLE  [F2]",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Modulate = new Color(0.4f, 0.8f, 1.0f)
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 16);
        headerHBox.AddChild(titleLabel);

        _closeButton = new Button
        {
            Text = " ✕ ",
            Modulate = new Color(1f, 0.5f, 0.5f)
        };
        _closeButton.Pressed += CloseConsole;
        headerHBox.AddChild(_closeButton);

        // 2. Main Two-Column Split
        var hsplit = new HSplitContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        outerVBox.AddChild(hsplit);

        // --- LEFT COLUMN: NPC Info & Controls ---
        var leftVBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(320, 0)
        };
        leftVBox.AddThemeConstantOverride("separation", 8);
        hsplit.AddChild(leftVBox);

        // NPC Selection Group
        var npcHeaderLabel = new Label { Text = "Target NPC:", Modulate = new Color(0.8f, 0.85f, 1f) };
        leftVBox.AddChild(npcHeaderLabel);

        var npcSelectHBox = new HBoxContainer();
        leftVBox.AddChild(npcSelectHBox);

        _npcDropdown = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            FitToLongestItem = false,
            CustomMinimumSize = new Vector2(180, 32)
        };
        _npcDropdown.ItemSelected += index =>
        {
            if (index >= 0 && index < _indexedNpcs.Count)
            {
                SelectNpc(_indexedNpcs[(int)index]);
            }
        };
        npcSelectHBox.AddChild(_npcDropdown);

        _pickLookedAtButton = new Button
        {
            Text = "🎯 Looked-At",
            TooltipText = "Select NPC the player is currently looking at"
        };
        _pickLookedAtButton.Pressed += () =>
        {
            var npc = FindLookedAtNpc();
            if (npc != null)
            {
                SelectNpc(npc);
            }
            else
            {
                ShowValidationMessage("[color=yellow]No NPC found in line of sight.[/color]", false);
            }
        };
        npcSelectHBox.AddChild(_pickLookedAtButton);

        // NPC Info Box
        var npcInfoPanel = new PanelContainer();
        var infoBoxStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.08f, 0.8f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6
        };
        npcInfoPanel.AddThemeStyleboxOverride("panel", infoBoxStyle);
        leftVBox.AddChild(npcInfoPanel);

        _npcInfoLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            CustomMinimumSize = new Vector2(300, 100)
        };
        npcInfoPanel.AddChild(_npcInfoLabel);

        // Example Library Selection
        var exampleHeaderLabel = new Label { Text = "Example Brain Library:", Modulate = new Color(0.8f, 0.85f, 1f) };
        leftVBox.AddChild(exampleHeaderLabel);

        _exampleDropdown = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            FitToLongestItem = false,
            CustomMinimumSize = new Vector2(180, 32)
        };
        _exampleDropdown.ItemSelected += _ => LoadSelectedExample();
        leftVBox.AddChild(_exampleDropdown);

        var exampleButtonsHBox = new HBoxContainer();
        leftVBox.AddChild(exampleButtonsHBox);

        _loadExampleButton = new Button
        {
            Text = "📂 Load Example",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _loadExampleButton.Pressed += LoadSelectedExample;
        exampleButtonsHBox.AddChild(_loadExampleButton);

        _loadCurrentScriptButton = new Button
        {
            Text = "📥 Current Script",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _loadCurrentScriptButton.Pressed += LoadCurrentNpcScript;
        exampleButtonsHBox.AddChild(_loadCurrentScriptButton);

        // Control Actions
        var actionsHeaderLabel = new Label { Text = "Execution Actions:", Modulate = new Color(0.8f, 0.85f, 1f) };
        leftVBox.AddChild(actionsHeaderLabel);

        _applyButton = new Button
        {
            Text = "▶  Apply Script to NPC",
            CustomMinimumSize = new Vector2(0, 38)
        };
        _applyButton.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.5f));
        _applyButton.Pressed += ApplyCurrentScript;
        leftVBox.AddChild(_applyButton);

        var actionSubHBox = new HBoxContainer();
        leftVBox.AddChild(actionSubHBox);

        _validateButton = new Button
        {
            Text = "✓ Validate Only",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _validateButton.Pressed += ValidateCurrentScriptOnly;
        actionSubHBox.AddChild(_validateButton);

        _restartButton = new Button
        {
            Text = "🔄 Restart",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _restartButton.Pressed += RestartSelectedBrain;
        actionSubHBox.AddChild(_restartButton);

        _stopButton = new Button
        {
            Text = "⏹ Stop",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _stopButton.Pressed += StopSelectedBrain;
        actionSubHBox.AddChild(_stopButton);

        // --- RIGHT COLUMN: Editor & Diagnostics ---
        var rightVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        rightVBox.AddThemeConstantOverride("separation", 6);
        hsplit.AddChild(rightVBox);

        _exampleInfoLabel = new Label
        {
            Text = "Select an example or paste custom Lua code below:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.9f, 0.7f)
        };
        rightVBox.AddChild(_exampleInfoLabel);

        // Script Editor
        _scriptEditor = new CodeEdit
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(580, 260),
            GuttersDrawLineNumbers = true,
            LineFolding = true,
            CodeCompletionEnabled = true
        };
        rightVBox.AddChild(_scriptEditor);

        // Output Tabs: Validation & Logs
        _outputTabs = new TabContainer
        {
            CustomMinimumSize = new Vector2(580, 150),
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        rightVBox.AddChild(_outputTabs);

        // Tab 1: Validation Results
        var valTab = new VBoxContainer { Name = "Validation Output" };
        _outputTabs.AddChild(valTab);

        _validationOutput = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ScrollFollowing = true,
            Text = "[color=lightgray]Click 'Validate Only' or 'Apply Script to NPC' to run validation.[/color]"
        };
        valTab.AddChild(_validationOutput);

        // Tab 2: Live Brain Logs
        var logTab = new VBoxContainer { Name = "NPC Brain Logs" };
        _outputTabs.AddChild(logTab);

        var logHeaderHBox = new HBoxContainer();
        logTab.AddChild(logHeaderHBox);

        var logTitle = new Label
        {
            Text = "Recent logs emitted by selected NPC:",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Modulate = new Color(0.6f, 0.8f, 1f)
        };
        logHeaderHBox.AddChild(logTitle);

        _clearLogsButton = new Button { Text = "Clear" };
        _clearLogsButton.Pressed += () => _logOutput.Text = string.Empty;
        logHeaderHBox.AddChild(_clearLogsButton);

        _logOutput = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ScrollFollowing = true
        };
        logTab.AddChild(_logOutput);
    }
}
