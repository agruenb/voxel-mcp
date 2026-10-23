namespace VoxelKing.Game.Player;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Voxel;
using VoxelKing.Game.Common;
using VoxelKing.Game.UI;

/// <summary>
/// First-person voxel player controller implementing mouse look, WASD movement, jumping, sprinting,
/// debug fly-mode toggling, smooth 1-block step climbing, surface spawning, voxel raycasting,
/// block breaking with hardness, block placing with collision checks, and chest interaction.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    [Export] public float WalkSpeed { get; set; } = 5.0f;
    [Export] public float SprintSpeed { get; set; } = 8.5f;
    [Export] public float JumpVelocity { get; set; } = 7.5f;
    [Export] public float Gravity { get; set; } = 20.0f;
    [Export] public float FlySpeed { get; set; } = 15.0f;
    [Export] public float SprintFlyMultiplier { get; set; } = 2.0f;
    [Export] public float MouseSensitivity { get; set; } = 0.003f;
    [Export] public float MaxStepHeight { get; set; } = 1.1f;
    [Export] public float EyeHeight { get; set; } = 1.6f;
    [Export] public float BodyRadius { get; set; } = 0.35f;
    [Export] public float BodyHeight { get; set; } = 1.8f;
    [Export] public float FloorSnapLengthValue { get; set; } = 0.4f;
    [Export] public bool EnableStepAssist { get; set; } = true;
    [Export] public bool EnableStepSmoothing { get; set; } = true;
    [Export] public bool FreezeUntilGrounded { get; set; } = true;
    [Export] public float ReachDistance { get; set; } = 5.0f;

    public bool IsFlying { get; set; } = false;
    public bool IsSprinting { get; private set; } = false;
    public bool IsGrounded { get; private set; } = false;
    public bool IsMouseCaptured { get; private set; } = false;
    public bool IsInputLocked { get; set; } = false;

    // Inventory & Hotbar (36 slots: 0..8 Hotbar, 9..35 Main)
    public Inventory Inventory { get; } = new(36);
    public int SelectedHotbarSlot { get; set; } = 0;
    public float BreakProgress { get; private set; } = 0f;
    public VoxelRaycastResult CurrentRaycast { get; private set; } = VoxelRaycastResult.Miss;

    // External dependencies
    public IVoxelWorld? VoxelWorld { get; set; }
    public IBlockRegistry? BlockRegistry { get; set; }
    public ContainerManager? ContainerManager { get; set; }
    public NamedPlaces? NamedPlaces { get; set; }
    public ChestUI? ChestUI { get; set; }
    public HotbarUI? HotbarUI { get; set; }
    public DialogueUI? DialogueUI { get; set; }
    public NpcManager? NpcManager { get; set; }
    public PlayerEntity? PlayerEntity { get; set; }

    public Node3D Head { get; private set; } = null!;
    public Camera3D Camera { get; private set; } = null!;
    public CollisionShape3D CollisionShape { get; private set; } = null!;
    public BlockHighlightNode HighlightNode { get; private set; } = null!;

    private float _pitch;
    private float _yaw;
    private float _headYOffset = 0f;
    private bool _wasOnFloor = false;
    private bool _hasGroundedOnce = false;
    private int _groundWaitTicks = 0;

    // Mining tracking
    private Vec3i? _currentlyMiningPos;
    private float _miningTimer = 0f;
    private bool _isMiningActionHeld = false;

    // Test simulation overrides (used when not null)
    public Vector2? SimulatedMoveInput { get; set; }
    public bool? SimulatedJump { get; set; }
    public bool? SimulatedSprint { get; set; }

    public PlayerController()
    {
    }

    public void ApplyConfig(PlayerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        WalkSpeed = config.WalkSpeed;
        SprintSpeed = config.SprintSpeed;
        JumpVelocity = config.JumpVelocity;
        Gravity = config.Gravity;
        FlySpeed = config.FlySpeed;
        SprintFlyMultiplier = config.SprintFlyMultiplier;
        MouseSensitivity = config.MouseSensitivity;
        MaxStepHeight = config.MaxStepHeight;
        EyeHeight = config.EyeHeight;
        BodyRadius = config.BodyRadius;
        BodyHeight = config.BodyHeight;
        EnableStepAssist = config.EnableStepAssist;
        EnableStepSmoothing = config.EnableStepSmoothing;
    }

    public override void _Ready()
    {
        EnsureComponents();

        _yaw = Rotation.Y;
        _pitch = Head.Rotation.X;

        FloorConstantSpeed = true;
        FloorStopOnSlope = true;
        FloorMaxAngle = Mathf.DegToRad(50.0f);
        FloorSnapLength = FloorSnapLengthValue;
        UpDirection = Vector3.Up;

        PopulateStartingInventory();

        // Capture mouse by default if not running in headless mode
        if (DisplayServer.GetName() != "headless")
        {
            CaptureMouse();
        }
    }

    private void PopulateStartingInventory()
    {
        if (!Inventory.IsEmpty) return;

        // Populate initial blocks for player building and chest testing
        Inventory.SetSlot(0, new ItemStack("dirt", 20));
        Inventory.SetSlot(1, new ItemStack("cobblestone", 20));
        Inventory.SetSlot(2, new ItemStack("oak_log", 10));
        Inventory.SetSlot(3, new ItemStack("planks", 20));
        Inventory.SetSlot(4, new ItemStack("chest", 4));
    }

    /// <summary>
    /// Ensures CollisionShape, Head, Camera, and HighlightNode are created and attached.
    /// </summary>
    public void EnsureComponents()
    {
        // 1. Collision shape
        CollisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape") ??
                         GetNodeOrNull<CollisionShape3D>("CollisionShape3D");

        if (CollisionShape == null)
        {
            CollisionShape = new CollisionShape3D { Name = "CollisionShape" };
            var capsule = new CapsuleShape3D
            {
                Radius = BodyRadius,
                Height = BodyHeight
            };
            CollisionShape.Shape = capsule;
            CollisionShape.Position = new Vector3(0, BodyHeight * 0.5f, 0);
            AddChild(CollisionShape);
        }

        // 2. Head pivot
        Head = GetNodeOrNull<Node3D>("Head");
        if (Head == null)
        {
            Head = new Node3D { Name = "Head", Position = new Vector3(0, EyeHeight, 0) };
            AddChild(Head);
        }

        // 3. Camera
        Camera = Head.GetNodeOrNull<Camera3D>("Camera3D") ??
                 Head.GetNodeOrNull<Camera3D>("Camera");

        if (Camera == null)
        {
            Camera = new Camera3D { Name = "Camera3D" };
            Head.AddChild(Camera);
        }

        // 4. Block highlight node
        HighlightNode = GetNodeOrNull<BlockHighlightNode>("BlockHighlight");
        if (HighlightNode == null)
        {
            HighlightNode = new BlockHighlightNode { Name = "BlockHighlight" };
            AddChild(HighlightNode);
        }
    }

    /// <summary>
    /// Positions the player safely on top of the surface terrain at the specified target coordinates.
    /// </summary>
    public void SpawnOnSurface(ITerrainGenerator terrainGenerator, int startX = 0, int startZ = 0, IVoxelWorld? voxelWorld = null)
    {
        ArgumentNullException.ThrowIfNull(terrainGenerator);

        Vec3 spawnVec = voxelWorld != null
            ? SurfaceSpawnFinder.FindSurfaceSpawn(terrainGenerator, voxelWorld, startX, startZ)
            : SurfaceSpawnFinder.FindSurfaceSpawn(terrainGenerator, startX, startZ);

        GlobalPosition = new Vector3(spawnVec.X, spawnVec.Y, spawnVec.Z);
        Velocity = Vector3.Zero;
        _hasGroundedOnce = false;
        _groundWaitTicks = 0;
    }

    public void CaptureMouse()
    {
        IsMouseCaptured = true;
        if (DisplayServer.GetName() != "headless")
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public void ReleaseMouse()
    {
        IsMouseCaptured = false;
        if (DisplayServer.GetName() != "headless")
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void ToggleMouseCapture()
    {
        if (IsMouseCaptured)
        {
            ReleaseMouse();
        }
        else
        {
            CaptureMouse();
        }
    }

    public void ToggleFly()
    {
        IsFlying = !IsFlying;
        Velocity = Vector3.Zero;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsInputLocked)
        {
            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            // Number keys 1..9 select hotbar slots 0..8
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key9)
            {
                SelectedHotbarSlot = (int)(keyEvent.Keycode - Key.Key1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.Escape)
            {
                ToggleMouseCapture();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.E)
            {
                if (DialogueUI != null && !DialogueUI.IsOpen)
                {
                    var lookedAtNpc = FindLookedAtNpc(DialogueUI.InteractionReachDistance);
                    if (lookedAtNpc != null)
                    {
                        DialogueUI.OpenDialogue(lookedAtNpc);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
            }

            if (keyEvent.Keycode == Key.F)
            {
                ToggleFly();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.Q && !keyEvent.Echo)
            {
                bool dropAll = keyEvent.CtrlPressed || keyEvent.ShiftPressed;
                DropSelectedItem(dropAll);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            // Mouse wheel cycles hotbar slot
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                SelectedHotbarSlot = (SelectedHotbarSlot + 8) % 9;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                SelectedHotbarSlot = (SelectedHotbarSlot + 1) % 9;
                GetViewport().SetInputAsHandled();
                return;
            }

            // Left click mining
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    if (!IsMouseCaptured)
                    {
                        CaptureMouse();
                    }
                    _isMiningActionHeld = true;
                }
                else
                {
                    _isMiningActionHeld = false;
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            // Right click place / interact
            if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                TryPlaceOrInteract();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseMotion mouseMotion && IsMouseCaptured)
        {
            _yaw -= mouseMotion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - (mouseMotion.Relative.Y * MouseSensitivity), -Mathf.Pi * 0.49f, Mathf.Pi * 0.49f);

            Rotation = new Vector3(0, _yaw, 0);
            Head.Rotation = new Vector3(_pitch, 0, 0);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (EnableStepSmoothing && Head != null && Mathf.Abs(_headYOffset) > 0.001f)
        {
            _headYOffset = Mathf.MoveToward(_headYOffset, 0f, (float)delta * 14.0f);
            Head.Position = new Vector3(0, EyeHeight + _headYOffset, 0);
        }

        // Perform raycast from camera view
        PerformRaycast();

        // Update block highlight visual
        if (HighlightNode != null)
        {
            HighlightNode.UpdateHighlight(CurrentRaycast.IsHit, CurrentRaycast.BlockPos.ToGodotVector3(), BreakProgress);
        }

        // Process mining if held
        if (!IsInputLocked && _isMiningActionHeld && CurrentRaycast.IsHit)
        {
            ProcessMining(CurrentRaycast, (float)delta);
        }
        else if (!_isMiningActionHeld || !CurrentRaycast.IsHit)
        {
            ResetMining();
        }

        // Update HUD dialogue interaction prompt if applicable
        if (DialogueUI != null && !IsInputLocked)
        {
            DialogueUI.UpdateTargetNpc(FindLookedAtNpc(DialogueUI.InteractionReachDistance));
        }
        else if (DialogueUI != null && DialogueUI.IsOpen)
        {
            DialogueUI.UpdateTargetNpc(null);
        }
    }

    /// <summary>
    /// Finds the active NPC closest to the crosshair center within the specified maximum distance,
    /// ensuring the NPC is not obstructed by solid voxels.
    /// </summary>
    public NpcState? FindLookedAtNpc(float maxDistance = 4.0f, float minDotProduct = 0.80f)
    {
        if (NpcManager == null)
        {
            return null;
        }

        var cam = Camera ?? Head?.GetNodeOrNull<Camera3D>("Camera3D") ?? GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (cam == null)
        {
            return null;
        }

        ForceUpdateTransform();
        Head?.ForceUpdateTransform();
        cam.ForceUpdateTransform();

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

            Vector3 npcCenter = new Vector3(npc.Position.X, npc.Position.Y + 0.9f, npc.Position.Z);
            Vector3 toNpc = npcCenter - camPos;
            float dist = toNpc.Length();

            if (dist > maxDistance || dist < 0.2f)
            {
                continue;
            }

            Vector3 dirToNpc = toNpc / dist;
            float dot = camFwd.Dot(dirToNpc);

            if (dot > bestDot)
            {
                // Occlusion check: if voxel raycast hit a block closer than NPC, it is occluded
                if (CurrentRaycast.IsHit && CurrentRaycast.Distance < (dist - 0.4f))
                {
                    continue;
                }

                bestDot = dot;
                bestNpc = npc;
            }
        }

        return bestNpc;
    }

    private void PerformRaycast()
    {
        if (Camera == null || VoxelWorld == null || BlockRegistry == null)
        {
            CurrentRaycast = VoxelRaycastResult.Miss;
            return;
        }

        ForceUpdateTransform();
        Head?.ForceUpdateTransform();
        Camera.ForceUpdateTransform();

        Vec3 origin = Camera.GlobalPosition.ToCore();
        Vec3 direction = (-Camera.GlobalTransform.Basis.Z).ToCore();

        CurrentRaycast = VoxelRaycast.Cast(origin, direction, ReachDistance, VoxelWorld, BlockRegistry);
    }

    public void TriggerMiningAction(bool isHeld)
    {
        _isMiningActionHeld = isHeld;
    }

    private void ProcessMining(VoxelRaycastResult target, float dt)
    {
        if (VoxelWorld == null || BlockRegistry == null) return;

        if (_currentlyMiningPos != target.BlockPos)
        {
            _currentlyMiningPos = target.BlockPos;
            _miningTimer = 0f;
            BreakProgress = 0f;
        }

        var def = BlockRegistry.GetDefinition(target.BlockId);
        float hardness = Math.Max(0.1f, def.Hardness);

        _miningTimer += dt;
        BreakProgress = Mathf.Clamp(_miningTimer / hardness, 0f, 1f);

        if (BreakProgress >= 1.0f)
        {
            CompleteMining(target, def);
        }
    }

    public void CompleteMining(VoxelRaycastResult target, BlockDefinition def)
    {
        if (VoxelWorld == null || BlockRegistry == null) return;

        // 1. Remove block from voxel world
        VoxelWorld.SetBlock(target.BlockPos, BlockRegistry.AirId);

        // 2. Handle container destruction
        if (def.IsContainer && ContainerManager != null)
        {
            if (ContainerManager.RemoveContainer(target.BlockPos, out var container))
            {
                // Transfer remaining items in chest into player inventory
                for (int s = 0; s < container.SlotCount; s++)
                {
                    var item = container.GetSlot(s);
                    if (item != null && !item.IsEmpty)
                    {
                        Inventory.AddItem(item.ItemId, item.Count, out _);
                    }
                }
            }

            if (NamedPlaces != null && NamedPlaces.GetPlace("player_chest") == target.BlockPos)
            {
                NamedPlaces.RemovePlace("player_chest");
            }
        }

        // 3. Drop item into player inventory
        if (def.DropItem != null)
        {
            Inventory.AddItem(def.DropItem, 1, out _);
        }

        ResetMining();
    }

    private void ResetMining()
    {
        _currentlyMiningPos = null;
        _miningTimer = 0f;
        BreakProgress = 0f;
    }

    /// <summary>
    /// Drops a single item (or entire stack if dropAll is true) from the currently selected hotbar slot.
    /// </summary>
    public bool DropSelectedItem(bool dropAll = false)
    {
        var slot = Inventory.GetSlot(SelectedHotbarSlot);
        if (slot == null || slot.IsEmpty) return false;

        int count = dropAll ? slot.Count : 1;
        return DropItem(SelectedHotbarSlot, count);
    }

    /// <summary>
    /// Drops an item stack or single item from the specified inventory slot into the world.
    /// </summary>
    public bool DropItem(int slotIndex, int count = 1)
    {
        if (count <= 0) return false;

        var slot = Inventory.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return false;

        int toDrop = Math.Min(count, slot.Count);
        string itemId = slot.ItemId;

        if (!Inventory.RemoveAt(slotIndex, toDrop))
        {
            return false;
        }

        Vector3 dropDir;
        Vector3 dropPos;
        if (Camera != null)
        {
            ForceUpdateTransform();
            Head?.ForceUpdateTransform();
            Camera.ForceUpdateTransform();
            dropDir = -Camera.GlobalTransform.Basis.Z.Normalized();
            dropPos = Camera.GlobalPosition + (dropDir * 0.4f);
        }
        else
        {
            dropDir = -GlobalTransform.Basis.Z.Normalized();
            dropPos = GlobalPosition + (Vector3.Up * EyeHeight) + (dropDir * 0.4f);
        }

        Vector3 velocity = (dropDir * 3.5f) + (Vector3.Up * 1.5f);

        if (NpcManager != null)
        {
            NpcManager.SpawnItem(
                new ItemStack(itemId, toDrop),
                dropPos.ToCore(),
                velocity.ToCore(),
                pickupCooldown: 1.0f);
        }

        return true;
    }

    /// <summary>
    /// Attempts to open a chest or place the selected hotbar block at the targeted raycast position.
    /// </summary>
    public bool TryPlaceOrInteract()
    {
        if (VoxelWorld == null || BlockRegistry == null || !CurrentRaycast.IsHit)
        {
            return false;
        }

        var targetDef = BlockRegistry.GetDefinition(CurrentRaycast.BlockId);

        // 1. If clicking on a chest, open Chest UI
        if (targetDef.IsContainer && ChestUI != null && ContainerManager != null)
        {
            var chestInv = ContainerManager.GetOrCreateContainer(CurrentRaycast.BlockPos, 27);
            ChestUI.OpenChest(CurrentRaycast.BlockPos, chestInv);
            return true;
        }

        // 2. Otherwise place block from active hotbar slot
        var selectedItem = Inventory.GetSlot(SelectedHotbarSlot);
        if (selectedItem == null || selectedItem.IsEmpty)
        {
            return false;
        }

        if (!BlockRegistry.TryGetId(selectedItem.ItemId, out ushort placeBlockId))
        {
            return false;
        }

        Vec3i placePos = CurrentRaycast.AdjacentPos;

        // Check if target placement voxel is currently non-solid (air/water)
        ushort currentBlock = VoxelWorld.GetBlock(placePos);
        if (BlockRegistry.GetDefinition(currentBlock).IsSolid)
        {
            return false;
        }

        // Check entity collision (cannot place block inside player body)
        Vec3 playerFeet = GlobalPosition.ToCore();
        if (BlockPlacementHelper.IntersectsEntity(placePos, playerFeet, BodyRadius, BodyHeight))
        {
            return false;
        }

        // Place block
        bool placed = VoxelWorld.SetBlock(placePos, placeBlockId);
        if (placed)
        {
            Inventory.RemoveAt(SelectedHotbarSlot, 1);

            // If placed block is chest, register in ContainerManager
            var placedDef = BlockRegistry.GetDefinition(placeBlockId);
            if (placedDef.IsContainer && ContainerManager != null)
            {
                ContainerManager.GetOrCreateContainer(placePos, 27);
            }
            return true;
        }

        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsInputLocked)
        {
            Velocity = Vector3.Zero;
            return;
        }

        float dt = (float)delta;
        IsGrounded = IsOnFloor();

        // Hold player vertically on cold spawn until chunk underneath has loaded collision
        if (FreezeUntilGrounded && !_hasGroundedOnce && !IsFlying)
        {
            bool floorNearby = TestMove(GlobalTransform, new Vector3(0, -0.6f, 0));
            if (floorNearby)
            {
                _hasGroundedOnce = true;
            }
            else if (_groundWaitTicks < 60)
            {
                _groundWaitTicks++;
                Velocity = Vector3.Zero;
                return;
            }
            else
            {
                _hasGroundedOnce = true;
            }
        }

        Vector2 inputVector = ReadMovementInput();
        bool jumpPressed = ReadJumpInput();
        IsSprinting = ReadSprintInput();

        Vector3 forward = -Transform.Basis.Z;
        Vector3 right = Transform.Basis.X;
        Vector3 moveDirection = (forward * inputVector.Y + right * inputVector.X);

        if (moveDirection.LengthSquared() > 1.0f)
        {
            moveDirection = moveDirection.Normalized();
        }

        if (IsFlying)
        {
            ProcessFlying(moveDirection, jumpPressed, dt);
        }
        else
        {
            ProcessWalking(moveDirection, jumpPressed, dt);
        }
    }

    private void ProcessFlying(Vector3 moveDirection, bool jumpPressed, float dt)
    {
        float speed = IsSprinting ? (FlySpeed * SprintFlyMultiplier) : FlySpeed;
        Vector3 flyVelocity = moveDirection * speed;

        if (jumpPressed)
        {
            flyVelocity.Y += speed;
        }

        if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.C) || Input.IsKeyPressed(Key.Shift))
        {
            flyVelocity.Y -= speed;
        }

        Velocity = flyVelocity;
        MoveAndSlide();
    }

    private void ProcessWalking(Vector3 moveDirection, bool jumpPressed, float dt)
    {
        Vector3 currentVelocity = Velocity;
        bool currentlyOnFloor = IsOnFloor();

        // 1. Gravity and jumping
        if (currentlyOnFloor)
        {
            if (jumpPressed)
            {
                currentVelocity.Y = JumpVelocity;
                FloorSnapLength = 0f;
            }
            else
            {
                currentVelocity.Y = -0.1f; // keep floor snap engaged
                FloorSnapLength = FloorSnapLengthValue;
            }
        }
        else
        {
            currentVelocity.Y -= Gravity * dt;
        }

        // 2. Horizontal movement
        float targetSpeed = IsSprinting ? SprintSpeed : WalkSpeed;
        Vector3 targetHorizontalVel = moveDirection * targetSpeed;

        float accel = currentlyOnFloor ? 25.0f : 10.0f;
        Vector3 currentHorizontalVel = new Vector3(currentVelocity.X, 0, currentVelocity.Z);
        Vector3 newHorizontalVel = currentHorizontalVel.MoveToward(targetHorizontalVel, accel * targetSpeed * dt);

        currentVelocity.X = newHorizontalVel.X;
        currentVelocity.Z = newHorizontalVel.Z;
        Velocity = currentVelocity;

        bool wasOnFloorBeforeMove = currentlyOnFloor;
        MoveAndSlide();

        // 3. Step assist: if we hit a step while walking on/near the ground
        if (EnableStepAssist && (wasOnFloorBeforeMove || IsOnFloor()) && IsOnWall() && moveDirection.LengthSquared() > 0.01f)
        {
            ApplyStepAssist(moveDirection, targetSpeed, dt);
        }

        _wasOnFloor = currentlyOnFloor;
    }

    private void ApplyStepAssist(Vector3 moveDirection, float targetSpeed, float dt)
    {
        Vector3 dir = moveDirection.Normalized();
        float stepForwardDist = Mathf.Max(targetSpeed * dt, 0.15f);
        Vector3 forwardMotion = dir * stepForwardDist;

        // Test 1: Check vertical headroom
        Vector3 upMotion = new Vector3(0, MaxStepHeight, 0);
        var upCol = new KinematicCollision3D();
        bool hitCeiling = TestMove(GlobalTransform, upMotion, upCol);
        float actualUp = MaxStepHeight;

        if (hitCeiling)
        {
            actualUp = upCol.GetTravel().Y;
            if (actualUp < 0.4f)
            {
                return;
            }
        }

        // Test 2: Check horizontal clearance at the raised height
        Transform3D raisedTransform = GlobalTransform.Translated(new Vector3(0, actualUp, 0));
        var fwdCol = new KinematicCollision3D();
        bool hitFwd = TestMove(raisedTransform, forwardMotion, fwdCol);
        Vector3 actualForward = hitFwd ? fwdCol.GetTravel() : forwardMotion;

        if (actualForward.LengthSquared() < 0.0001f)
        {
            return;
        }

        // Test 3: Cast downward to detect step floor surface
        Transform3D forwardTransform = raisedTransform.Translated(actualForward);
        var downCol = new KinematicCollision3D();
        Vector3 downMotion = new Vector3(0, -(actualUp + 0.15f), 0);
        bool hitFloor = TestMove(forwardTransform, downMotion, downCol);

        if (!hitFloor)
        {
            return;
        }

        if (downCol.GetNormal().AngleTo(Vector3.Up) > FloorMaxAngle)
        {
            return;
        }

        float stepDownTravel = downCol.GetTravel().Y;
        float stepUpHeight = actualUp + stepDownTravel;

        if (stepUpHeight > 0.01f && stepUpHeight <= MaxStepHeight)
        {
            GlobalPosition += new Vector3(actualForward.X, stepUpHeight, actualForward.Z);

            if (EnableStepSmoothing && Head != null)
            {
                _headYOffset -= stepUpHeight;
            }
        }
    }

    private Vector2 ReadMovementInput()
    {
        if (SimulatedMoveInput.HasValue)
        {
            return SimulatedMoveInput.Value;
        }

        float x = 0f;
        float y = 0f;

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) || IsActionActive("move_forward"))
            y += 1.0f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) || IsActionActive("move_backward"))
            y -= 1.0f;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) || IsActionActive("move_left"))
            x -= 1.0f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) || IsActionActive("move_right"))
            x += 1.0f;

        return new Vector2(x, y);
    }

    private bool ReadJumpInput()
    {
        if (SimulatedJump.HasValue)
        {
            return SimulatedJump.Value;
        }

        return Input.IsKeyPressed(Key.Space) || IsActionActive("jump");
    }

    private bool ReadSprintInput()
    {
        if (SimulatedSprint.HasValue)
        {
            return SimulatedSprint.Value;
        }

        return Input.IsKeyPressed(Key.Shift) || IsActionActive("sprint");
    }

    private static bool IsActionActive(StringName actionName)
    {
        return InputMap.HasAction(actionName) && Input.IsActionPressed(actionName);
    }
}
