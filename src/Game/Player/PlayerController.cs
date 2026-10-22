namespace VoxelKing.Game.Player;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// First-person voxel player controller implementing mouse look, WASD movement, jumping, sprinting,
/// debug fly-mode toggling, smooth 1-block step climbing, and surface spawning.
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

    public bool IsFlying { get; set; } = false;
    public bool IsSprinting { get; private set; } = false;
    public bool IsGrounded { get; private set; } = false;
    public bool IsMouseCaptured { get; private set; } = false;

    public Node3D Head { get; private set; } = null!;
    public Camera3D Camera { get; private set; } = null!;
    public CollisionShape3D CollisionShape { get; private set; } = null!;

    private float _pitch;
    private float _yaw;
    private float _headYOffset = 0f;
    private bool _wasOnFloor = false;
    private bool _hasGroundedOnce = false;
    private int _groundWaitTicks = 0;

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

        // Capture mouse by default if not running in headless mode
        if (DisplayServer.GetName() != "headless")
        {
            CaptureMouse();
        }
    }

    /// <summary>
    /// Ensures CollisionShape, Head, and Camera nodes are created and attached if not present in the scene.
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
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                ToggleMouseCapture();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.F)
            {
                ToggleFly();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                CaptureMouse();
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
    }

    public override void _PhysicsProcess(double delta)
    {
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

        if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.C) || Input.IsKeyPressed(Key.Q))
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
                return; // ceiling too low to step up
            }
        }

        // Test 2: Check horizontal clearance at the raised height
        Transform3D raisedTransform = GlobalTransform.Translated(new Vector3(0, actualUp, 0));
        var fwdCol = new KinematicCollision3D();
        bool hitFwd = TestMove(raisedTransform, forwardMotion, fwdCol);
        Vector3 actualForward = hitFwd ? fwdCol.GetTravel() : forwardMotion;

        if (actualForward.LengthSquared() < 0.0001f)
        {
            return; // blocked forward (e.g. wall higher than step height)
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
