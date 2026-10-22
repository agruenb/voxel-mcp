namespace VoxelKing.Core.Tests.Npc;

using System;
using System.Collections.Generic;
using System.Linq;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Speech;
using Xunit;

public class SpeechSystemTests
{
    [Fact]
    public void Speak_ValidSpeech_PublishesEntitySpokeEvent()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus);

        EntitySpoke? captured = null;
        using var sub = eventBus.Subscribe<EntitySpoke>(e => captured = e);

        var speakerId = new EntityId(10);
        var pos = new Vec3(5f, 64f, 5f);
        bool result = speech.Speak(speakerId, "Bob", pos, "Hello world!", hearingRadius: 16f, timestamp: 1.0);

        Assert.True(result);
        Assert.NotNull(captured);
        Assert.Equal(speakerId, captured.Value.SpeakerId);
        Assert.Equal("Bob", captured.Value.SpeakerName);
        Assert.Equal(pos, captured.Value.Position);
        Assert.Equal("Hello world!", captured.Value.Text);
        Assert.Equal(16f, captured.Value.HearingRadius);
    }

    [Fact]
    public void Speak_RateLimiting_DropsSecondCallWithinInterval()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus, rateLimitSeconds: 2.0f);

        var speakerId = new EntityId(10);
        var pos = new Vec3(0, 0, 0);

        bool first = speech.Speak(speakerId, "Bob", pos, "First message", timestamp: 10.0);
        bool second = speech.Speak(speakerId, "Bob", pos, "Second message", timestamp: 11.5); // only 1.5s later

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void Speak_AfterRateLimitExpires_Succeeds()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus, rateLimitSeconds: 2.0f);

        var speakerId = new EntityId(10);
        var pos = new Vec3(0, 0, 0);

        bool first = speech.Speak(speakerId, "Bob", pos, "First message", timestamp: 10.0);
        bool second = speech.Speak(speakerId, "Bob", pos, "Second message", timestamp: 12.1); // 2.1s later

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public void Speak_TruncatesTextOverMaxLimit()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus, maxTextLength: 200);

        EntitySpoke? captured = null;
        using var sub = eventBus.Subscribe<EntitySpoke>(e => captured = e);

        string longText = new string('A', 300);
        var speakerId = new EntityId(1);
        speech.Speak(speakerId, "Bob", Vec3.Zero, longText, timestamp: 0.0);

        Assert.NotNull(captured);
        Assert.Equal(200, captured.Value.Text.Length);
        Assert.Equal(new string('A', 200), captured.Value.Text);
    }

    [Fact]
    public void Speak_EmptyOrWhitespace_ReturnsFalse()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus);

        Assert.False(speech.Speak(new EntityId(1), "Bob", Vec3.Zero, ""));
        Assert.False(speech.Speak(new EntityId(1), "Bob", Vec3.Zero, "   "));
    }

    [Fact]
    public void Speak_LogsSpokeToSpeakerMemory_AndHeardToNearbyListeners()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus);

        var speaker = new NpcState(new EntityId(1), "Alice", new Vec3(0, 64, 0));
        var listenerClose = new NpcState(new EntityId(2), "Bob", new Vec3(10, 64, 0)); // 10 blocks away (< 16)
        var listenerFar = new NpcState(new EntityId(3), "Charlie", new Vec3(30, 64, 0)); // 30 blocks away (> 16)

        registry.Register(speaker);
        registry.Register(listenerClose);
        registry.Register(listenerFar);

        bool result = speech.Speak(speaker.Id, speaker.Name, speaker.Position, "Hey Bob!", hearingRadius: 16f, timestamp: 5.0);

        Assert.True(result);

        // Speaker should have recorded speech spoke
        var spokeEvents = speaker.Memory.GetAllEvents().OfType<SpeechSpokeEvent>().ToList();
        Assert.Single(spokeEvents);
        Assert.Equal("Hey Bob!", spokeEvents[0].Text);
        Assert.Equal(5.0, spokeEvents[0].Timestamp);

        // Close listener should have recorded speech heard
        var heardClose = listenerClose.Memory.GetAllEvents().OfType<SpeechHeardEvent>().ToList();
        Assert.Single(heardClose);
        Assert.Equal("Alice", heardClose[0].Speaker);
        Assert.Equal("Hey Bob!", heardClose[0].Text);
        Assert.False(heardClose[0].IsPlayer);

        // Speaker should NOT have recorded speech heard for its own speech
        var heardSelf = speaker.Memory.GetAllEvents().OfType<SpeechHeardEvent>().ToList();
        Assert.Empty(heardSelf);

        // Far listener should NOT have recorded speech heard
        var heardFar = listenerFar.Memory.GetAllEvents().OfType<SpeechHeardEvent>().ToList();
        Assert.Empty(heardFar);
    }

    [Fact]
    public void Speak_DistinguishesPlayerFromNpcInHeardEvent()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus);

        var player = new PlayerEntity(new EntityId(99), "Player1", new Vec3(0, 64, 0));
        var listener = new NpcState(new EntityId(2), "Bob", new Vec3(5, 64, 0));

        registry.Register(player);
        registry.Register(listener);

        speech.Speak(player.Id, player.Name, player.Position, "Hello NPC", timestamp: 1.0);

        var heard = listener.Memory.GetAllEvents().OfType<SpeechHeardEvent>().ToList();
        Assert.Single(heard);
        Assert.Equal("Player1", heard[0].Speaker);
        Assert.True(heard[0].IsPlayer);
    }

    [Fact]
    public void BrainHost_ReceivesOnHeard_WhenWithinRadius()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus);

        var speaker = new NpcState(new EntityId(1), "Alice", new Vec3(0, 64, 0));
        var listener = new NpcState(new EntityId(2), "Bob", new Vec3(5, 64, 0));

        registry.Register(speaker);
        registry.Register(listener);

        var brain = new BrainHost(listener, entityRegistry: registry, eventBus: eventBus);
        listener.BrainHost = brain;

        // Script with an on_heard handler that sets memory
        string script = @"
function on_heard(speaker_id, text)
    memory.set('heard_speaker', speaker_id)
    memory.set('heard_text', text)
end

function main()
    while true do
        npc.wait(1)
    end
end
";
        brain.LoadScript(script);
        brain.Tick(0.1f); // Initialize coroutine

        // Alice speaks
        speech.Speak(speaker.Id, speaker.Name, speaker.Position, "Greetings from Alice!", hearingRadius: 16f, timestamp: 1.0);

        // Advance brain tick to process queued event
        brain.Tick(0.1f);

        Assert.Equal("Greetings from Alice!", listener.Memory.Get("heard_text"));
        Assert.Equal(speaker.Id.Value, Convert.ToInt64(listener.Memory.Get("heard_speaker")));
    }

    [Fact]
    public void BrainHost_DoesNotReceiveOnHeard_WhenOutsideRadius()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var speech = new SpeechSystem(registry, eventBus);

        var speaker = new NpcState(new EntityId(1), "Alice", new Vec3(0, 64, 0));
        var listener = new NpcState(new EntityId(2), "Bob", new Vec3(50, 64, 0)); // 50 blocks away (> 16)

        registry.Register(speaker);
        registry.Register(listener);

        var brain = new BrainHost(listener, entityRegistry: registry, eventBus: eventBus);
        listener.BrainHost = brain;

        string script = @"
function on_heard(speaker_id, text)
    local count = memory.get('heard_count', 0)
    memory.set('heard_count', count + 1)
end

function main()
    while true do
        npc.wait(1)
    end
end
";
        brain.LoadScript(script);
        brain.Tick(0.1f);

        speech.Speak(speaker.Id, speaker.Name, speaker.Position, "Can you hear me?", hearingRadius: 16f, timestamp: 1.0);
        brain.Tick(0.1f);

        Assert.False(listener.Memory.Has("heard_count"));
    }

    [Fact]
    public void NpcManager_PhysicsTick_AdvancesSpeechSystemCurrentTime()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        using var manager = new NpcManager(registry, eventBus: eventBus);

        Assert.NotNull(manager.SpeechSystem);
        Assert.Equal(0.0, manager.SpeechSystem.CurrentTime);

        manager.PhysicsTick(0.25f);
        Assert.Equal(0.25, manager.SpeechSystem.CurrentTime, precision: 3);

        manager.PhysicsTick(0.75f);
        Assert.Equal(1.0, manager.SpeechSystem.CurrentTime, precision: 3);
    }

    [Fact]
    public void NpcManager_Dispose_DisposesSpeechSystem()
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        var manager = new NpcManager(registry, eventBus: eventBus);

        var speechSystem = manager.SpeechSystem;
        Assert.NotNull(speechSystem);

        manager.Dispose();

        // After disposal, publishing an event should not throw or process
        eventBus.Publish(new EntitySpoke(new EntityId(1), "Test", Vec3.Zero, "Hello", 16f));
    }
}
