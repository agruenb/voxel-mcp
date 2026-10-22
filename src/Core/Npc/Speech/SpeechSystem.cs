namespace VoxelKing.Core.Npc.Speech;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Authoritative implementation of <see cref="ISpeechSystem"/> managing speech broadcasts,
/// rate-limiting, range filtering, and memory logging (ARCHITECTURE §5.6).
/// </summary>
public sealed class SpeechSystem : ISpeechSystem
{
    public const float StandardHearingRadius = 16.0f;
    public const float StandardRateLimitSeconds = 2.0f;
    public const int StandardMaxTextLength = 200;

    private readonly EntityRegistry _registry;
    private readonly IEventBus _eventBus;
    private readonly ILogger? _logger;
    private readonly Dictionary<EntityId, double> _lastSpeechTimes = new();
    private readonly IDisposable? _eventSubscription;
    private bool _isProcessingInternalPublish;
    private bool _disposed;

    /// <inheritdoc />
    public float DefaultHearingRadius { get; set; } = StandardHearingRadius;

    /// <inheritdoc />
    public float RateLimitSeconds { get; set; } = StandardRateLimitSeconds;

    /// <inheritdoc />
    public int MaxTextLength { get; set; } = StandardMaxTextLength;

    /// <inheritdoc />
    public double CurrentTime { get; set; }

    public SpeechSystem(
        EntityRegistry registry,
        IEventBus eventBus,
        ILogger? logger = null,
        float hearingRadius = StandardHearingRadius,
        float rateLimitSeconds = StandardRateLimitSeconds,
        int maxTextLength = StandardMaxTextLength)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger;
        DefaultHearingRadius = hearingRadius;
        RateLimitSeconds = rateLimitSeconds;
        MaxTextLength = maxTextLength;

        _eventSubscription = _eventBus.Subscribe<EntitySpoke>(OnExternalEntitySpoke);
    }

    /// <inheritdoc />
    public bool Speak(
        EntityId speakerId,
        string speakerName,
        Vec3 position,
        string text,
        float? hearingRadius = null,
        double? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        double time = timestamp ?? CurrentTime;
        float radius = hearingRadius ?? DefaultHearingRadius;

        // 1. Rate limiting: 1 per RateLimitSeconds per entity
        if (_lastSpeechTimes.TryGetValue(speakerId, out double lastTime))
        {
            if (time - lastTime < RateLimitSeconds)
            {
                return false;
            }
        }

        _lastSpeechTimes[speakerId] = time;

        // 2. Character truncation to MaxTextLength (default 200)
        string sanitized = text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        // 3. Update speaker NPC state and memory
        if (_registry.TryGet(speakerId, out var speakerEntity) && speakerEntity is NpcState speakerNpc)
        {
            speakerNpc.LastSayTime = (float)time;
            speakerNpc.Memory.RecordSpeechSpoke(sanitized, time);
        }

        // 4. Broadcast EntitySpoke on EventBus
        _isProcessingInternalPublish = true;
        try
        {
            _eventBus.Publish(new EntitySpoke(speakerId, speakerName, position, sanitized, radius));
        }
        finally
        {
            _isProcessingInternalPublish = false;
        }

        // 5. Deliver to listeners within hearing radius and record in their memory
        DeliverToNearbyListeners(speakerId, speakerName, position, sanitized, radius, time);

        _logger?.LogDebug($"SpeechSystem: '{speakerName}' (ID {speakerId}) spoke: \"{sanitized}\" (radius={radius})");
        return true;
    }

    private void OnExternalEntitySpoke(EntitySpoke spoke)
    {
        if (_isProcessingInternalPublish)
        {
            return; // Internal publish already processed delivery
        }

        DeliverToNearbyListeners(spoke.SpeakerId, spoke.SpeakerName, spoke.Position, spoke.Text, spoke.HearingRadius, CurrentTime);
    }

    private void DeliverToNearbyListeners(
        EntityId speakerId,
        string speakerName,
        Vec3 position,
        string text,
        float radius,
        double time)
    {
        bool isPlayer = _registry.TryGet(speakerId, out var speaker) && speaker.Kind == EntityKind.Player;

        var nearbyEntities = _registry.FindAll(position, radius);
        foreach (var entity in nearbyEntities)
        {
            if (entity.Id == speakerId)
            {
                continue;
            }

            if (entity is NpcState listenerNpc)
            {
                listenerNpc.Memory.RecordSpeechHeard(speakerName, text, isPlayer, time);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _eventSubscription?.Dispose();
    }
}
