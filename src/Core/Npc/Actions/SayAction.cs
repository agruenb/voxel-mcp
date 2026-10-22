namespace VoxelKing.Core.Npc.Actions;

using System;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Instantaneous overlay action making an NPC speak aloud with hearing radius and rate limiting.
/// </summary>
public sealed class SayAction : ActionBase
{
    public const int MaxTextLength = 200;
    public const float RateLimitSeconds = 2.0f;
    public const float DefaultHearingRadius = 16.0f;

    public override string Name => "Say";

    public NpcState Npc { get; }
    public string Text { get; }
    public IEventBus? EventBus { get; }
    public float HearingRadius { get; }
    public float CurrentTime { get; }
    public bool WasSpoken { get; private set; }

    public SayAction(
        NpcState npc,
        string text,
        IEventBus? eventBus = null,
        float hearingRadius = DefaultHearingRadius,
        float currentTime = 0f)
        : base(1f)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        Text = text ?? "";
        EventBus = eventBus;
        HearingRadius = hearingRadius;
        CurrentTime = currentTime;
    }

    protected override void OnStart()
    {
        string sanitized = Text;
        if (sanitized.Length > MaxTextLength)
        {
            sanitized = sanitized[..MaxTextLength];
        }

        // Rate limiting: 1 per 2 seconds. Extra calls are dropped silently per LUA_BRAIN_API §3.3.
        if (CurrentTime - Npc.LastSayTime < RateLimitSeconds)
        {
            WasSpoken = false;
            Succeed();
            return;
        }

        Npc.LastSayTime = CurrentTime;
        WasSpoken = true;

        EventBus?.Publish(new EntitySpoke(Npc.Id, Npc.Name, Npc.Position, sanitized, HearingRadius));
        Succeed();
    }
}
