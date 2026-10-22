namespace VoxelKing.Game.UI;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Common;
using VoxelKing.Game.Player;

/// <summary>
/// Interactive dialogue UI panel and HUD prompt for NPC conversations (F-405, ARCHITECTURE §5.6).
/// - Shows HUD prompt "[E] Talk to <NPC>" when looking at an NPC within 4 blocks.
/// - Opens interactive dialogue window with recent conversation history and text input.
/// - Transitions NPC conversation states: Listening -> Thinking -> Replying.
/// - Supports closing/cancelling with Escape and instructing multiple NPCs concurrently while thinking.
/// </summary>
public partial class DialogueUI : CanvasLayer
{
    public const float InteractionReachDistance = 4.0f;

    public bool IsOpen => _rootPanel != null && _rootPanel.Visible;
    public NpcState? CurrentNpc => _currentNpc;
    public LineEdit InputField => _inputField;

    private PlayerController? _player;
    private NpcManager? _npcManager;
    private IBrainCompiler? _brainCompiler;
    private PlayerEntity? _playerEntity;
    private NpcState? _currentNpc;
    private NpcState? _targetLookedAtNpc;

    // UI Nodes
    private Control _rootPanel = null!;
    private PanelContainer _promptPanel = null!;
    private Label _promptLabel = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Label _taskBadge = null!;
    private VBoxContainer _conversationList = null!;
    private ScrollContainer _scrollContainer = null!;
    private LineEdit _inputField = null!;
    private Button _sendButton = null!;
    private Button _closeButton = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        Layer = 15; // Placed above standard HUD elements
        BuildUI();
        _rootPanel.Visible = false;
        _promptPanel.Visible = false;
    }

    public void Setup(
        PlayerController player,
        NpcManager npcManager,
        IBrainCompiler brainCompiler,
        PlayerEntity? playerEntity = null)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _npcManager = npcManager ?? throw new ArgumentNullException(nameof(npcManager));
        _brainCompiler = brainCompiler ?? throw new ArgumentNullException(nameof(brainCompiler));
        _playerEntity = playerEntity;
    }

    /// <summary>
    /// Updates the currently aimed-at NPC from the player controller to display or hide the HUD prompt.
    /// Also keeps the listening NPC facing the player while dialogue is active.
    /// </summary>
    public void UpdateTargetNpc(NpcState? npc)
    {
        _targetLookedAtNpc = npc;

        if (IsOpen)
        {
            _promptPanel.Visible = false;
            if (_currentNpc != null && _player != null)
            {
                // Smoothly update facing direction towards player while listening
                Vec3 toPlayer = _player.GlobalPosition.ToCore() - _currentNpc.Position;
                _currentNpc.SetFacingDirection(toPlayer);
            }
            return;
        }

        if (_targetLookedAtNpc != null && _targetLookedAtNpc.IsActive)
        {
            _promptLabel.Text = $"💬 [E] Talk to {_targetLookedAtNpc.Name}";
            _promptPanel.Visible = true;
        }
        else
        {
            _promptPanel.Visible = false;
        }
    }

    /// <summary>
    /// Opens the dialogue panel for the specified NPC, pausing its action and facing the player.
    /// </summary>
    public void OpenDialogue(NpcState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (IsOpen)
        {
            CloseDialogue(cancelled: true);
        }

        _currentNpc = npc;
        _currentNpc.SetConversationState(NpcConversationState.Listening);

        if (_player != null)
        {
            Vec3 toPlayer = _player.GlobalPosition.ToCore() - _currentNpc.Position;
            _currentNpc.SetFacingDirection(toPlayer);

            _player.IsInputLocked = true;
            _player.ReleaseMouse();
        }

        // Header info
        _titleLabel.Text = $"💬 {_currentNpc.Name}";

        string personality = string.IsNullOrWhiteSpace(_currentNpc.Personality)
            ? (!string.IsNullOrWhiteSpace(_currentNpc.Profile) ? _currentNpc.Profile : "Friendly Villager")
            : _currentNpc.Personality;
        _subtitleLabel.Text = personality;

        if (!string.IsNullOrWhiteSpace(_currentNpc.CurrentTaskSummary))
        {
            _taskBadge.Text = $"Task: {_currentNpc.CurrentTaskSummary}";
            _taskBadge.Visible = true;
        }
        else
        {
            _taskBadge.Text = "Task: Idle";
            _taskBadge.Visible = true;
        }

        RefreshConversationHistory();

        _statusLabel.Text = "Press Enter to Send  •  Press Esc to Cancel";
        _inputField.Text = string.Empty;
        _inputField.Editable = true;
        _sendButton.Disabled = false;

        _promptPanel.Visible = false;
        _rootPanel.Visible = true;

        _inputField.GrabFocus();
    }

    /// <summary>
    /// Closes the dialogue panel and restores player input and mouse capture.
    /// </summary>
    public void CloseDialogue(bool cancelled)
    {
        if (!IsOpen)
        {
            return;
        }

        if (cancelled && _currentNpc != null)
        {
            if (_currentNpc.ConversationState == NpcConversationState.Listening)
            {
                _currentNpc.SetConversationState(NpcConversationState.None);
            }
            else if (_currentNpc.ConversationState == NpcConversationState.Thinking)
            {
                _brainCompiler?.Cancel(_currentNpc.Id);
                _currentNpc.SetConversationState(NpcConversationState.None);
            }
        }

        if (_player != null)
        {
            _player.IsInputLocked = false;
            _player.CaptureMouse();
        }

        _rootPanel.Visible = false;
        _currentNpc = null;
    }

    /// <summary>
    /// Submits the current instruction, closing the dialogue panel immediately so the player
    /// can freely move and interact with other NPCs while this NPC enters the Thinking state.
    /// </summary>
    public void SubmitInstruction()
    {
        if (_currentNpc == null)
        {
            return;
        }

        string text = _inputField.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var targetNpc = _currentNpc;
        string instructorName = _playerEntity?.Name ?? "Player";
        EntityId? instructorId = _playerEntity?.Id;

        // Immediately close dialogue panel to allow the player to move or talk to other NPCs
        CloseDialogue(cancelled: false);

        // Put NPC into Thinking state
        targetNpc.SetConversationState(NpcConversationState.Thinking);

        // Trigger compilation in background task (BrainCompiler handles thread dispatching back to main)
        _ = Task.Run(async () =>
        {
            try
            {
                if (_brainCompiler != null)
                {
                    await _brainCompiler.CompileAsync(
                        targetNpc,
                        text,
                        instructorId: instructorId,
                        instructorName: instructorName,
                        autoApply: true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"BrainCompiler error for NPC {targetNpc.Id} ({targetNpc.Name}): {ex.Message}");
                if (targetNpc.ConversationState == NpcConversationState.Thinking)
                {
                    targetNpc.SetConversationState(NpcConversationState.None);
                }
            }
        });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                CloseDialogue(cancelled: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter)
            {
                SubmitInstruction();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    private void RefreshConversationHistory()
    {
        foreach (var child in _conversationList.GetChildren())
        {
            child.QueueFree();
        }

        if (_currentNpc == null || _currentNpc.ConversationHistory.Count == 0)
        {
            var placeholder = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                Text = "[color=#8892a0][i]No previous conversation. Enter an instruction or talk to this NPC below.[/i][/color]"
            };
            placeholder.AddThemeFontSizeOverride("normal_font_size", 13);
            _conversationList.AddChild(placeholder);
            return;
        }

        foreach (var turn in _currentNpc.ConversationHistory)
        {
            var label = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("normal_font_size", 13);

            string safeSpeaker = turn.Speaker.Replace("[", "").Replace("]", "");
            string safeMsg = turn.Message.Replace("[", "").Replace("]", "");

            if (string.Equals(safeSpeaker, "Player", StringComparison.OrdinalIgnoreCase) ||
                (_playerEntity != null && string.Equals(safeSpeaker, _playerEntity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                label.Text = $"[color=#5dade2][b]{safeSpeaker}:[/b][/color] \"{safeMsg}\"";
            }
            else
            {
                label.Text = $"[color=#f5b041][b]{safeSpeaker}:[/b][/color] \"{safeMsg}\"";
            }

            _conversationList.AddChild(label);
        }

        // Auto-scroll to latest conversation turn
        CallDeferred(nameof(ScrollToBottom));
    }

    private void ScrollToBottom()
    {
        if (_scrollContainer != null)
        {
            _scrollContainer.ScrollVertical = (int)_scrollContainer.GetVScrollBar().MaxValue;
        }
    }

    private void BuildUI()
    {
        // 1. HUD Interaction Prompt (centered near bottom)
        _promptPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.65f,
            AnchorBottom = 0.65f,
            OffsetLeft = -140,
            OffsetRight = 140,
            OffsetTop = 0,
            OffsetBottom = 38
        };

        var promptStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.14f, 0.85f),
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderColor = new Color(0.35f, 0.60f, 0.95f, 0.9f)
        };
        _promptPanel.AddThemeStyleboxOverride("panel", promptStyle);

        _promptLabel = new Label
        {
            Text = "💬 [E] Talk to NPC",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _promptLabel.AddThemeFontSizeOverride("font_size", 14);
        _promptLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.96f, 1.0f));
        _promptPanel.AddChild(_promptLabel);
        AddChild(_promptPanel);

        // 2. Main Dialogue Panel Container
        _rootPanel = new Control
        {
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f
        };
        AddChild(_rootPanel);

        // Dark dim backdrop
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.45f),
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f
        };
        _rootPanel.AddChild(backdrop);

        // Center dialog window
        var dialog = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -280,
            OffsetRight = 280,
            OffsetTop = -220,
            OffsetBottom = 220
        };

        var dialogStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.13f, 0.17f, 0.98f),
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderColor = new Color(0.28f, 0.35f, 0.48f, 0.95f)
        };
        dialog.AddThemeStyleboxOverride("panel", dialogStyle);
        _rootPanel.AddChild(dialog);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        dialog.AddChild(margin);

        var mainVBox = new VBoxContainer();
        mainVBox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(mainVBox);

        // Header Top Row
        var headerRow = new HBoxContainer();
        _titleLabel = new Label
        {
            Text = "💬 NPC Name",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.98f, 1f));
        headerRow.AddChild(_titleLabel);

        _taskBadge = new Label
        {
            Text = "Task: Idle"
        };
        _taskBadge.AddThemeFontSizeOverride("font_size", 12);
        _taskBadge.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.65f, 1f));
        headerRow.AddChild(_taskBadge);

        _closeButton = new Button
        {
            Text = " ✕ ",
            FocusMode = Control.FocusModeEnum.None
        };
        _closeButton.Pressed += () => CloseDialogue(cancelled: true);
        headerRow.AddChild(_closeButton);
        mainVBox.AddChild(headerRow);

        // Subtitle (Personality / Skills)
        _subtitleLabel = new Label
        {
            Text = "Personality description",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 12);
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.70f, 0.78f, 1f));
        mainVBox.AddChild(_subtitleLabel);

        // Divider
        mainVBox.AddChild(new HSeparator());

        // Conversation History Scroll Box
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _scrollContainer.CustomMinimumSize = new Vector2(0, 200);

        _conversationList = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _conversationList.AddThemeConstantOverride("separation", 6);
        _scrollContainer.AddChild(_conversationList);
        mainVBox.AddChild(_scrollContainer);

        // Divider
        mainVBox.AddChild(new HSeparator());

        // Input Row
        var inputRow = new HBoxContainer();
        inputRow.AddThemeConstantOverride("separation", 8);

        _inputField = new LineEdit
        {
            PlaceholderText = "Give an instruction or talk to this NPC...",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _inputField.AddThemeFontSizeOverride("font_size", 14);
        _inputField.TextSubmitted += _ => SubmitInstruction();
        inputRow.AddChild(_inputField);

        _sendButton = new Button
        {
            Text = "Send ➤",
            CustomMinimumSize = new Vector2(80, 0)
        };
        _sendButton.Pressed += SubmitInstruction;
        inputRow.AddChild(_sendButton);
        mainVBox.AddChild(inputRow);

        // Bottom Status / Key Hints
        _statusLabel = new Label
        {
            Text = "Press Enter to Send  •  Press Esc to Cancel",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 11);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.50f, 0.55f, 0.65f, 1f));
        mainVBox.AddChild(_statusLabel);
    }
}
