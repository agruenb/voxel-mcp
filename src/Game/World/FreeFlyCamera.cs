namespace VoxelKing.Game.World;

using Godot;

/// <summary>
/// Lightweight free-fly spectator camera for viewing and navigating the voxel world.
/// Supports WASD + Space/Shift movement and mouse-look rotation.
/// </summary>
public partial class FreeFlyCamera : Camera3D
{
    [Export] public float MoveSpeed { get; set; } = 20.0f;
    [Export] public float FastMultiplier { get; set; } = 3.0f;
    [Export] public float MouseSensitivity { get; set; } = 0.003f;

    private float _pitch;
    private float _yaw;
    private bool _isMouseCaptured;

    public override void _Ready()
    {
        _yaw = Rotation.Y;
        _pitch = Rotation.X;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                if (mouseButton.Pressed)
                {
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                    _isMouseCaptured = true;
                }
                else
                {
                    Input.MouseMode = Input.MouseModeEnum.Visible;
                    _isMouseCaptured = false;
                }
            }
        }
        else if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _isMouseCaptured = false;
        }
        else if (@event is InputEventMouseMotion motion && _isMouseCaptured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - (motion.Relative.Y * MouseSensitivity), -Mathf.Pi * 0.49f, Mathf.Pi * 0.49f);
            Rotation = new Vector3(_pitch, _yaw, 0f);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var inputDirection = Vector3.Zero;

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) inputDirection += -Basis.Z;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) inputDirection += Basis.Z;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) inputDirection += -Basis.X;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) inputDirection += Basis.X;
        if (Input.IsKeyPressed(Key.Space) || Input.IsKeyPressed(Key.E)) inputDirection += Vector3.Up;
        if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Q)) inputDirection += Vector3.Down;

        float speed = MoveSpeed;
        if (Input.IsKeyPressed(Key.Shift))
        {
            speed *= FastMultiplier;
        }

        if (inputDirection != Vector3.Zero)
        {
            GlobalPosition += inputDirection.Normalized() * speed * dt;
        }
    }
}
