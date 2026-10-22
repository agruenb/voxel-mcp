namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;

/// <summary>
/// Instantaneous overlay action turning an NPC's head and body to face a target position.
/// </summary>
public sealed class LookAtAction : ActionBase
{
    public override string Name => "LookAt";

    public NpcState Npc { get; }
    public Vec3 TargetPos { get; }

    public LookAtAction(NpcState npc, Vec3 targetPos)
        : base(1f)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        TargetPos = targetPos;
    }

    public LookAtAction(NpcState npc, Vec3i targetBlockPos)
        : this(npc, new Vec3(targetBlockPos.X + 0.5f, targetBlockPos.Y + 0.5f, targetBlockPos.Z + 0.5f))
    {
    }

    protected override void OnStart()
    {
        var direction = TargetPos - Npc.Position;
        if (direction.LengthSquared() > 1e-4f)
        {
            Npc.SetFacingDirection(direction);
        }

        Succeed();
    }
}
