namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Action making an NPC wait/idle in place for a specified duration in seconds.
/// </summary>
public sealed class WaitAction : ActionBase
{
    public const float MaxWaitSeconds = 600.0f;

    public override string Name => "Wait";

    public NpcState Npc { get; }
    public float Duration { get; }

    public WaitAction(NpcState npc, float seconds)
        : base(0f)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        Duration = seconds;
    }

    protected override void OnStart()
    {
        if (Duration < 0f)
        {
            Fail(ActionFailureReason.InvalidTarget);
            return;
        }

        Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);

        if (Duration == 0f)
        {
            Succeed();
        }
    }

    protected override void OnTick(float dt)
    {
        Npc.Velocity = new Vec3(0f, Npc.Velocity.Y, 0f);

        float waitDuration = Math.Min(Duration, MaxWaitSeconds);
        if (ElapsedTime >= waitDuration)
        {
            Succeed();
        }
    }
}
