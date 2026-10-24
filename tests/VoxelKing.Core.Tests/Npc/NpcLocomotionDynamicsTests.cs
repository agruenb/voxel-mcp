namespace VoxelKing.Core.Tests.Npc;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using Xunit;

public class NpcLocomotionDynamicsTests
{
    [Fact]
    public void CalculateTurnBank_TurningRight_ProducesNegativeBankWithinLimits()
    {
        // Negative angular velocity = turning right
        float bank = NpcLocomotionDynamics.CalculateTurnBank(-0.5f);

        Assert.True(bank < 0f, "Banking into a right turn must be negative roll.");
        Assert.InRange(bank, -NpcLocomotionDynamics.MaxBankRadians, -0.05f);
    }

    [Fact]
    public void CalculateTurnBank_TurningLeft_ProducesPositiveBankWithinLimits()
    {
        // Positive angular velocity = turning left
        float bank = NpcLocomotionDynamics.CalculateTurnBank(0.5f);

        Assert.True(bank > 0f, "Banking into a left turn must be positive roll.");
        Assert.InRange(bank, 0.05f, NpcLocomotionDynamics.MaxBankRadians);
    }

    [Fact]
    public void CalculateTurnBank_RapidSpin_ClampsToMaxBank()
    {
        float bank = NpcLocomotionDynamics.CalculateTurnBank(5.0f);
        Assert.Equal(NpcLocomotionDynamics.MaxBankRadians, bank, precision: 4);

        float bankNeg = NpcLocomotionDynamics.CalculateTurnBank(-5.0f);
        Assert.Equal(-NpcLocomotionDynamics.MaxBankRadians, bankNeg, precision: 4);
    }

    [Fact]
    public void CalculateTurnBank_ZeroVelocity_ReturnsZero()
    {
        float bank = NpcLocomotionDynamics.CalculateTurnBank(0f);
        Assert.Equal(0f, bank);
    }

    [Fact]
    public void CalculateForwardLean_StationaryOrSlow_ReturnsZero()
    {
        Assert.Equal(0f, NpcLocomotionDynamics.CalculateForwardLean(0f));
        Assert.Equal(0f, NpcLocomotionDynamics.CalculateForwardLean(0.08f));
    }

    [Fact]
    public void CalculateForwardLean_Running_ReturnsForwardPitchWithinLimits()
    {
        float lean = NpcLocomotionDynamics.CalculateForwardLean(5.0f);

        // Negative pitch tilts forward in Godot coordinate frame
        Assert.True(lean < 0f, "Running forward lean should produce negative pitch.");
        Assert.InRange(lean, -NpcLocomotionDynamics.MaxForwardLeanRadians, -0.02f);
    }

    [Fact]
    public void CalculateForwardLean_FastSprint_ClampsToMaxLean()
    {
        float lean = NpcLocomotionDynamics.CalculateForwardLean(15.0f);
        Assert.Equal(-NpcLocomotionDynamics.MaxForwardLeanRadians, lean, precision: 4);
    }

    [Fact]
    public void CalculateLandingSquash_AtTouchdown_ProducesMaximumCompression()
    {
        var squash = NpcLocomotionDynamics.CalculateLandingSquash(0f, duration: 0.15f, maxSquash: 0.08f);

        Assert.Equal(0.92f, squash.Y, precision: 2);
        Assert.Equal(1.08f, squash.X, precision: 2);
        Assert.Equal(1.08f, squash.Z, precision: 2);
    }

    [Fact]
    public void CalculateLandingSquash_AtCompletion_ReturnsUnitScale()
    {
        var squash = NpcLocomotionDynamics.CalculateLandingSquash(0.15f, duration: 0.15f);
        Assert.Equal(Vec3.One, squash);

        var squashPast = NpcLocomotionDynamics.CalculateLandingSquash(0.20f, duration: 0.15f);
        Assert.Equal(Vec3.One, squashPast);
    }

    [Fact]
    public void CalculateAirbornePose_Ascending_RaisesArmsAndExtendsLegs()
    {
        var pose = NpcLocomotionDynamics.CalculateAirbornePose(5.0f);

        // Negative RotX raises arms forward
        Assert.True(pose.LeftArmRotX < -0.3f);
        Assert.True(pose.RightArmRotX < -0.3f);
        // Legs extended downwards / trailing
        Assert.True(pose.LeftLegRotX > 0f);
    }

    [Fact]
    public void CalculateAirbornePose_Falling_FlaresArmsOutward()
    {
        var pose = NpcLocomotionDynamics.CalculateAirbornePose(-6.0f);

        // Arms flared outward: Left arm negative RotZ, Right arm positive RotZ
        Assert.True(pose.LeftArmRotZ < -0.3f, "Left arm should flare outwards to the left.");
        Assert.True(pose.RightArmRotZ > 0.3f, "Right arm should flare outwards to the right.");
    }
}
