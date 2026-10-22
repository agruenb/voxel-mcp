namespace VoxelKing.Core.Common;

/// <summary>
/// Configuration parameters for first-person player controller movement, camera, and physics.
/// </summary>
public sealed record PlayerConfig
{
    public float WalkSpeed { get; set; } = 5.0f;
    public float SprintSpeed { get; set; } = 8.5f;
    public float JumpVelocity { get; set; } = 7.5f;
    public float Gravity { get; set; } = 20.0f;
    public float FlySpeed { get; set; } = 15.0f;
    public float SprintFlyMultiplier { get; set; } = 2.0f;
    public float MouseSensitivity { get; set; } = 0.003f;
    public float MaxStepHeight { get; set; } = 1.1f;
    public float EyeHeight { get; set; } = 1.6f;
    public float BodyRadius { get; set; } = 0.35f;
    public float BodyHeight { get; set; } = 1.8f;
    public bool EnableStepAssist { get; set; } = true;
    public bool EnableStepSmoothing { get; set; } = true;
}
