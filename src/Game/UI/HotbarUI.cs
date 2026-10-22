namespace VoxelKing.Game.UI;

using Godot;
using VoxelKing.Core.Entities;
using VoxelKing.Game.Player;

/// <summary>
/// First-person HUD presenting screen crosshair, 9 hotbar inventory slots, breaking progress, and notifications.
/// </summary>
public partial class HotbarUI : CanvasLayer
{
    private PlayerController? _player;
    private readonly PanelContainer[] _slotPanels = new PanelContainer[9];
    private readonly Label[] _nameLabels = new Label[9];
    private readonly Label[] _countLabels = new Label[9];
    private ProgressBar _breakProgressBar = null!;
    private Label _notificationLabel = null!;
    private double _notificationTimer = 0;

    public void BindPlayer(PlayerController player)
    {
        if (_player != null)
        {
            _player.Inventory.Changed -= OnInventoryChanged;
        }

        _player = player;

        if (_player != null)
        {
            _player.Inventory.Changed += OnInventoryChanged;
            UpdateHotbarVisuals();
        }
    }

    public override void _Ready()
    {
        BuildUI();
        if (_player != null)
        {
            UpdateHotbarVisuals();
        }
    }

    public override void _ExitTree()
    {
        if (_player != null)
        {
            _player.Inventory.Changed -= OnInventoryChanged;
        }
    }

    private void BuildUI()
    {
        // 1. Crosshair at center
        var crosshair = new Label
        {
            Text = "+",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -15,
            OffsetTop = -15,
            OffsetRight = 15,
            OffsetBottom = 15
        };
        crosshair.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.7f));
        crosshair.AddThemeFontSizeOverride("font_size", 24);
        AddChild(crosshair);

        // 2. Notification label (centered below crosshair)
        _notificationLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.6f,
            AnchorBottom = 0.6f,
            OffsetLeft = -250,
            OffsetRight = 250,
            OffsetTop = -20,
            OffsetBottom = 20,
            Visible = false
        };
        _notificationLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f, 1f));
        _notificationLabel.AddThemeFontSizeOverride("font_size", 18);
        AddChild(_notificationLabel);

        // 3. Break progress bar
        _breakProgressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1.0,
            Value = 0,
            ShowPercentage = false,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.55f,
            AnchorBottom = 0.55f,
            OffsetLeft = -60,
            OffsetRight = 60,
            OffsetTop = -6,
            OffsetBottom = 6,
            Visible = false
        };
        AddChild(_breakProgressBar);

        // 4. Hotbar Container at bottom
        var hotbarBox = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -270,
            OffsetRight = 270,
            OffsetTop = -68,
            OffsetBottom = -12
        };
        hotbarBox.AddThemeConstantOverride("separation", 6);
        AddChild(hotbarBox);

        for (int i = 0; i < 9; i++)
        {
            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(54, 54)
            };

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.15f, 0.85f),
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.8f)
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var slotContent = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            slotContent.AddThemeConstantOverride("separation", 0);

            var keyBadge = new Label
            {
                Text = $"{i + 1}",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            keyBadge.AddThemeFontSizeOverride("font_size", 10);
            keyBadge.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
            slotContent.AddChild(keyBadge);

            var nameLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                ClipText = true
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f, 1f));
            slotContent.AddChild(nameLabel);
            _nameLabels[i] = nameLabel;

            var countLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            countLabel.AddThemeFontSizeOverride("font_size", 11);
            countLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f, 1f));
            slotContent.AddChild(countLabel);
            _countLabels[i] = countLabel;

            panel.AddChild(slotContent);
            hotbarBox.AddChild(panel);
            _slotPanels[i] = panel;
        }
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        // Update break progress bar
        float bp = _player.BreakProgress;
        if (bp > 0.01f)
        {
            _breakProgressBar.Visible = true;
            _breakProgressBar.Value = bp;
        }
        else
        {
            _breakProgressBar.Visible = false;
        }

        // Notification timer
        if (_notificationTimer > 0)
        {
            _notificationTimer -= delta;
            if (_notificationTimer <= 0)
            {
                _notificationLabel.Visible = false;
            }
        }

        UpdateHotbarVisuals();
    }

    public void ShowNotification(string message, double durationSeconds = 3.0)
    {
        _notificationLabel.Text = message;
        _notificationLabel.Visible = true;
        _notificationTimer = durationSeconds;
    }

    private void OnInventoryChanged()
    {
        UpdateHotbarVisuals();
    }

    public void UpdateHotbarVisuals()
    {
        if (_player == null) return;

        int selected = _player.SelectedHotbarSlot;

        for (int i = 0; i < 9; i++)
        {
            var item = _player.Inventory.GetSlot(i);
            _nameLabels[i].Text = item != null ? FormatItemName(item.ItemId) : "";
            _countLabels[i].Text = (item != null && item.Count > 1) ? $"{item.Count}" : "";

            var style = (StyleBoxFlat)_slotPanels[i].GetThemeStylebox("panel");
            if (i == selected)
            {
                style.BorderColor = new Color(1.0f, 0.82f, 0.2f, 1.0f); // Bright Gold selection border
                style.BorderWidthBottom = 3;
                style.BorderWidthLeft = 3;
                style.BorderWidthRight = 3;
                style.BorderWidthTop = 3;
                style.BgColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
            }
            else
            {
                style.BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                style.BorderWidthBottom = 2;
                style.BorderWidthLeft = 2;
                style.BorderWidthRight = 2;
                style.BorderWidthTop = 2;
                style.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.85f);
            }
        }
    }

    private static string FormatItemName(string itemId)
    {
        return itemId switch
        {
            "oak_log" => "log",
            "oak_leaves" => "leaves",
            "cobblestone" => "cobble",
            "coal_ore" => "coal",
            _ => itemId
        };
    }
}
