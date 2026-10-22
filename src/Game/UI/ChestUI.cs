namespace VoxelKing.Game.UI;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Game.Player;

/// <summary>
/// Modal dialog for interacting with chest containers, transferring items, and marking "player_chest".
/// </summary>
public partial class ChestUI : CanvasLayer
{
    private PlayerController? _player;
    private Inventory? _chestInventory;
    private Vec3i _chestPos;
    private NamedPlaces? _namedPlaces;
    private HotbarUI? _hotbarUI;

    private Control _rootPanel = null!;
    private GridContainer _chestGrid = null!;
    private GridContainer _playerGrid = null!;
    private Button _markChestButton = null!;
    private Label _titleLabel = null!;

    public bool IsOpen => _rootPanel != null && _rootPanel.Visible;

    public void Setup(PlayerController player, NamedPlaces namedPlaces, HotbarUI? hotbarUI = null)
    {
        _player = player;
        _namedPlaces = namedPlaces;
        _hotbarUI = hotbarUI;
    }

    public override void _Ready()
    {
        BuildUI();
        _rootPanel.Visible = false;
    }

    public void OpenChest(Vec3i chestPos, Inventory chestInventory)
    {
        _chestPos = chestPos;
        _chestInventory = chestInventory;

        if (_chestInventory != null)
        {
            _chestInventory.Changed += RefreshSlots;
        }

        if (_player != null)
        {
            _player.Inventory.Changed += RefreshSlots;
            _player.IsInputLocked = true;
            _player.ReleaseMouse();
        }

        _titleLabel.Text = $"Chest at ({chestPos.X}, {chestPos.Y}, {chestPos.Z})";
        UpdateMarkButtonState();
        RefreshSlots();

        _rootPanel.Visible = true;
    }

    public void CloseChest()
    {
        if (!IsOpen) return;

        if (_chestInventory != null)
        {
            _chestInventory.Changed -= RefreshSlots;
        }

        if (_player != null)
        {
            _player.Inventory.Changed -= RefreshSlots;
            _player.IsInputLocked = false;
            _player.CaptureMouse();
        }

        _rootPanel.Visible = false;
        _chestInventory = null;
    }

    private void UpdateMarkButtonState()
    {
        if (_namedPlaces == null) return;

        var currentMarked = _namedPlaces.GetPlace("player_chest");
        if (currentMarked.HasValue && currentMarked.Value == _chestPos)
        {
            _markChestButton.Text = "★ Marked as My Chest";
            _markChestButton.Disabled = true;
        }
        else
        {
            _markChestButton.Text = "☆ Mark as My Chest";
            _markChestButton.Disabled = false;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape || keyEvent.Keycode == Key.E)
            {
                CloseChest();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void BuildUI()
    {
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

        // Center dialog container
        var dialog = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -280,
            OffsetRight = 280,
            OffsetTop = -240,
            OffsetBottom = 240
        };

        var dialogStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.14f, 0.17f, 0.98f),
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderColor = new Color(0.35f, 0.35f, 0.4f, 0.9f)
        };
        dialog.AddThemeStyleboxOverride("panel", dialogStyle);
        _rootPanel.AddChild(dialog);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        dialog.AddChild(margin);

        var contentVBox = new VBoxContainer();
        contentVBox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(contentVBox);

        // Top bar
        var topBar = new HBoxContainer();
        _titleLabel = new Label
        {
            Text = "Chest",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 16);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f, 1f));
        topBar.AddChild(_titleLabel);

        _markChestButton = new Button
        {
            Text = "☆ Mark as My Chest"
        };
        _markChestButton.Pressed += OnMarkChestPressed;
        topBar.AddChild(_markChestButton);

        var closeBtn = new Button { Text = "✕" };
        closeBtn.Pressed += CloseChest;
        topBar.AddChild(closeBtn);
        contentVBox.AddChild(topBar);

        // Chest grid (3 rows x 9 columns = 27 slots)
        _chestGrid = new GridContainer
        {
            Columns = 9
        };
        _chestGrid.AddThemeConstantOverride("h_separation", 4);
        _chestGrid.AddThemeConstantOverride("v_separation", 4);
        contentVBox.AddChild(_chestGrid);

        // Divider
        var divider = new HSeparator();
        contentVBox.AddChild(divider);

        var playerHeader = new Label
        {
            Text = "Player Inventory (Click an item to transfer)"
        };
        playerHeader.AddThemeFontSizeOverride("font_size", 13);
        playerHeader.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1f));
        contentVBox.AddChild(playerHeader);

        // Player grid (first 9 slots = hotbar, followed by 27 inventory slots = 4 rows x 9 columns)
        _playerGrid = new GridContainer
        {
            Columns = 9
        };
        _playerGrid.AddThemeConstantOverride("h_separation", 4);
        _playerGrid.AddThemeConstantOverride("v_separation", 4);
        contentVBox.AddChild(_playerGrid);
    }

    private void OnMarkChestPressed()
    {
        if (_namedPlaces == null) return;

        _namedPlaces.SetPlace("player_chest", _chestPos);
        UpdateMarkButtonState();

        _hotbarUI?.ShowNotification($"★ Marked chest at ({_chestPos.X}, {_chestPos.Y}, {_chestPos.Z}) as player_chest!");
    }

    public void RefreshSlots()
    {
        if (_chestInventory == null || _player == null) return;

        // Clear existing slot buttons
        foreach (var child in _chestGrid.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var child in _playerGrid.GetChildren())
        {
            child.QueueFree();
        }

        // 1. Build Chest Slots
        for (int i = 0; i < _chestInventory.SlotCount; i++)
        {
            int slotIdx = i;
            var item = _chestInventory.GetSlot(slotIdx);
            var btn = CreateSlotButton(item, isHotbar: false);
            btn.Pressed += () =>
            {
                if (_chestInventory != null && _player != null)
                {
                    Inventory.Transfer(_chestInventory, slotIdx, _player.Inventory);
                }
            };
            _chestGrid.AddChild(btn);
        }

        // 2. Build Player Slots (Hotbar 0..8, then Main 9..35)
        for (int i = 0; i < _player.Inventory.SlotCount; i++)
        {
            int slotIdx = i;
            var item = _player.Inventory.GetSlot(slotIdx);
            var btn = CreateSlotButton(item, isHotbar: slotIdx < 9);
            btn.Pressed += () =>
            {
                if (_chestInventory != null && _player != null)
                {
                    Inventory.Transfer(_player.Inventory, slotIdx, _chestInventory);
                }
            };
            _playerGrid.AddChild(btn);
        }
    }

    private static Button CreateSlotButton(ItemStack? item, bool isHotbar)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(50, 48),
            ClipText = true
        };

        if (item != null && !item.IsEmpty)
        {
            btn.Text = item.Count > 1 ? $"{item.ItemId}\nx{item.Count}" : item.ItemId;
            btn.TooltipText = $"{item.ItemId} ({item.Count})";
        }
        else
        {
            btn.Text = "";
            btn.TooltipText = "Empty";
        }

        btn.AddThemeFontSizeOverride("font_size", 10);

        if (isHotbar)
        {
            btn.Modulate = new Color(1.0f, 1.0f, 0.9f, 1.0f);
        }

        return btn;
    }
}
