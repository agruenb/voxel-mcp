namespace VoxelKing.Game.UI;

using System;
using System.Collections.Generic;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// 2D Chat Log UI panel displayed on the player HUD (ARCHITECTURE §5.6).
/// Subscribes to EntitySpoke events, formatting and displaying chat messages with
/// semi-transparent styling and auto-scroll.
/// </summary>
public partial class ChatLogUI : CanvasLayer
{
    public const int MaxMessages = 25;

    public PanelContainer BackgroundPanel { get; private set; } = null!;
    public ScrollContainer Scroll { get; private set; } = null!;
    public VBoxContainer MessageList { get; private set; } = null!;

    private readonly List<RichTextLabel> _labels = new();
    private IDisposable? _spokeSub;
    private double _timeSinceLastMessage = 999.0;

    public override void _Ready()
    {
        Layer = 10; // Overlay layer
        EnsureHierarchyBuilt();
    }

    public void Initialize(IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        _spokeSub = eventBus.Subscribe<EntitySpoke>(OnEntitySpoke);
    }

    public void AddMessage(string speakerName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        EnsureHierarchyBuilt();

        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(340, 0)
        };

        label.AddThemeFontSizeOverride("normal_font_size", 14);
        label.AddThemeFontSizeOverride("bold_font_size", 14);

        // Sanitize bracket BBCode in user text to prevent injection
        string escapedText = text.Replace("[", "[lb]");
        label.Text = $"[color=#5dade2][b]{speakerName}[/b][/color]: {escapedText}";

        MessageList.AddChild(label);
        _labels.Add(label);

        while (_labels.Count > MaxMessages)
        {
            var oldest = _labels[0];
            _labels.RemoveAt(0);
            oldest.QueueFree();
        }

        _timeSinceLastMessage = 0.0;
        if (BackgroundPanel != null)
        {
            BackgroundPanel.Modulate = Colors.White;
        }

        // Auto-scroll to bottom on next frame
        CallDeferred(nameof(ScrollToBottom));
    }

    private void ScrollToBottom()
    {
        if (Scroll != null)
        {
            Scroll.ScrollVertical = (int)Scroll.GetVScrollBar().MaxValue;
        }
    }

    private void OnEntitySpoke(EntitySpoke e)
    {
        AddMessage(e.SpeakerName, e.Text);
    }

    public override void _Process(double delta)
    {
        _timeSinceLastMessage += delta;

        // Subtle dimming if no messages for > 10 seconds
        if (_timeSinceLastMessage > 10.0 && BackgroundPanel != null)
        {
            float targetAlpha = 0.35f;
            float currentAlpha = BackgroundPanel.Modulate.A;
            if (currentAlpha > targetAlpha)
            {
                BackgroundPanel.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(currentAlpha, targetAlpha, (float)(2.0 * delta)));
            }
        }
    }

    private void EnsureHierarchyBuilt()
    {
        if (BackgroundPanel != null)
        {
            return;
        }

        var rootControl = new Control
        {
            Name = "ChatRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rootControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rootControl);

        BackgroundPanel = new PanelContainer
        {
            Name = "ChatPanel",
            CustomMinimumSize = new Vector2(360, 160),
            Position = new Vector2(20, 220), // Positioned cleanly on left side below crosshair/stats
            MouseFilter = Control.MouseFilterEnum.Pass
        };

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.14f, 0.75f),
            BorderColor = new Color(0.20f, 0.26f, 0.36f, 0.60f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginBottom = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8
        };
        BackgroundPanel.AddThemeStyleboxOverride("panel", styleBox);
        rootControl.AddChild(BackgroundPanel);

        Scroll = new ScrollContainer
        {
            Name = "ChatScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        Scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        BackgroundPanel.AddChild(Scroll);

        MessageList = new VBoxContainer
        {
            Name = "MessageList",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        MessageList.AddThemeConstantOverride("separation", 4);
        Scroll.AddChild(MessageList);
    }

    public override void _ExitTree()
    {
        _spokeSub?.Dispose();
        _spokeSub = null;
    }
}
