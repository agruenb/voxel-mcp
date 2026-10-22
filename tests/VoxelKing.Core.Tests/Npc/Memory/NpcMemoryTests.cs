namespace VoxelKing.Core.Tests.Npc.Memory;

using System;
using System.Collections.Generic;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using Xunit;

public class NpcMemoryTests
{
    #region RingBuffer Tests

    [Fact]
    public void RingBuffer_AddWithinCapacity_MaintainsChronologicalOrder()
    {
        var buffer = new RingBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        Assert.Equal(3, buffer.Count);
        Assert.Equal(5, buffer.Capacity);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(2, buffer[1]);
        Assert.Equal(3, buffer[2]);

        var list = new List<int>(buffer);
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void RingBuffer_ExceedingCapacity_EvictsOldestFirst()
    {
        var buffer = new RingBuffer<string>(3);
        buffer.Add("a");
        buffer.Add("b");
        buffer.Add("c");
        buffer.Add("d"); // Evicts "a"
        buffer.Add("e"); // Evicts "b"

        Assert.Equal(3, buffer.Count);
        Assert.Equal("c", buffer[0]);
        Assert.Equal("d", buffer[1]);
        Assert.Equal("e", buffer[2]);

        var list = new List<string>(buffer);
        Assert.Equal(new[] { "c", "d", "e" }, list);
    }

    [Fact]
    public void RingBuffer_Clear_ResetsCount()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Clear();

        Assert.Empty(buffer);
    }

    [Fact]
    public void RingBuffer_InvalidCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(-5));
    }

    [Fact]
    public void RingBuffer_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[1]);
    }

    #endregion

    #region KV Store Limits & Validation Tests

    [Fact]
    public void KvStore_SetValidTypes_SuccessfullyStoredAndRetrieved()
    {
        var memory = new NpcMemory();

        memory.Set("flag", true);
        memory.Set("count", 42);
        memory.Set("rate", 3.14);
        memory.Set("name", "Bob");
        memory.Set("pos", new Vec3i(1, 2, 3));

        var dict = new Dictionary<string, object>
        {
            ["key1"] = "val1",
            ["key2"] = 100,
            ["key3"] = true,
            ["key4"] = new Vec3i(4, 5, 6)
        };
        memory.Set("flat_dict", dict);

        var list = new List<object> { "item1", 123, false, new Vec3i(7, 8, 9) };
        memory.Set("flat_list", list);

        Assert.Equal(7, memory.KeyCount);
        Assert.True(memory.Get<bool>("flag", false));
        Assert.Equal(42, memory.Get<int>("count", 0));
        Assert.Equal(3.14, memory.Get<double>("rate", 0.0), 2);
        Assert.Equal("Bob", memory.Get<string>("name", ""));
        Assert.Equal(new Vec3i(1, 2, 3), memory.Get<Vec3i>("pos", Vec3i.Zero));

        var retrievedDict = memory.Get("flat_dict") as IDictionary<string, object>;
        Assert.NotNull(retrievedDict);
        Assert.Equal("val1", retrievedDict!["key1"]);

        var retrievedList = memory.Get("flat_list") as IList<object>;
        Assert.NotNull(retrievedList);
        Assert.Equal("item1", retrievedList![0]);
    }

    [Fact]
    public void KvStore_MaxKeysLimit_Enforces64Keys()
    {
        var memory = new NpcMemory();

        for (int i = 0; i < NpcMemory.MaxKeys; i++)
        {
            memory.Set($"key_{i}", i);
        }

        Assert.Equal(64, memory.KeyCount);

        // 65th new key must fail
        var ex = Assert.Throws<InvalidOperationException>(() => memory.Set("key_overflow", 999));
        Assert.Contains("Memory key limit exceeded", ex.Message);

        // Updating an existing key succeeds
        memory.Set("key_0", 12345);
        Assert.Equal(12345, memory.Get<int>("key_0", 0));

        // Deleting a key allows adding a new key
        Assert.True(memory.Delete("key_0"));
        Assert.Equal(63, memory.KeyCount);
        memory.Set("new_key", "allowed");
        Assert.Equal("allowed", memory.Get<string>("new_key", ""));
    }

    [Fact]
    public void KvStore_MaxSizeLimit_Enforces16KB()
    {
        var memory = new NpcMemory();

        // Add 4 entries of 3,800 bytes each -> 15,200 bytes + key overhead
        for (int i = 0; i < 4; i++)
        {
            memory.Set($"chunk_{i}", new string('x', 3800));
        }

        // Adding another 3,800 byte entry will exceed 16 KB total size limit
        var ex = Assert.Throws<InvalidOperationException>(() => memory.Set("chunk_overflow", new string('y', 3800)));
        Assert.Contains("Memory size limit exceeded", ex.Message);
    }

    [Fact]
    public void KvStore_SingleStringValueLength_EnforcesMaxStringValueLength()
    {
        var memory = new NpcMemory();
        string tooLongString = new string('s', NpcMemory.MaxStringValueLength + 1);

        var ex = Assert.Throws<ArgumentException>(() => memory.Set("too_long", tooLongString));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    [Fact]
    public void KvStore_ValueTypeValidation_RejectsNestedCollections()
    {
        var memory = new NpcMemory();

        // Nested dictionary inside dictionary
        var nestedDict = new Dictionary<string, object>
        {
            ["inner"] = new Dictionary<string, object> { ["a"] = 1 }
        };
        Assert.Throws<ArgumentException>(() => memory.Set("invalid_dict", nestedDict));

        // Nested list inside list
        var nestedList = new List<object>
        {
            new List<object> { "nested" }
        };
        Assert.Throws<ArgumentException>(() => memory.Set("invalid_list", nestedList));

        // Unsupported arbitrary object
        Assert.Throws<ArgumentException>(() => memory.Set("arbitrary", new System.IO.MemoryStream()));
    }

    [Fact]
    public void KvStore_KeyValidation()
    {
        var memory = new NpcMemory();

        Assert.Throws<ArgumentException>(() => memory.Set("", 1));
        Assert.Throws<ArgumentException>(() => memory.Set("   ", 1));

        string tooLongKey = new string('k', NpcMemory.MaxKeyLength + 1);
        Assert.Throws<ArgumentException>(() => memory.Set(tooLongKey, 1));
    }

    [Fact]
    public void KvStore_SetNull_RemovesKey()
    {
        var memory = new NpcMemory();
        memory.Set("temp", "hello");
        Assert.True(memory.Has("temp"));

        memory.Set("temp", null);
        Assert.False(memory.Has("temp"));
        Assert.Equal(0, memory.KeyCount);
        Assert.Equal(0, memory.TotalSizeBytes);
    }

    [Fact]
    public void KvStore_TypedGet_PerformsNumericConversions()
    {
        var memory = new NpcMemory();
        memory.Set("num", 10.0);

        Assert.Equal(10, memory.Get<int>("num", 0));
        Assert.Equal(10L, memory.Get<long>("num", 0L));
        Assert.Equal(10.0f, memory.Get<float>("num", 0f));
        Assert.Equal(10.0, memory.Get<double>("num", 0.0));
        Assert.Equal("10", memory.Get<string>("num", ""));

        // Non-existent key returns default
        Assert.Equal(999, memory.Get<int>("missing", 999));
    }

    [Fact]
    public void KvStore_ClearKeys_ResetsState()
    {
        var memory = new NpcMemory();
        memory.Set("a", 1);
        memory.Set("b", 2);

        memory.ClearKeys();
        Assert.Equal(0, memory.KeyCount);
        Assert.Equal(0, memory.TotalSizeBytes);
        Assert.False(memory.Has("a"));
    }

    #endregion

    #region Named Places Tests

    [Fact]
    public void NamedPlaces_RememberAndRetrieve_Succeeds()
    {
        var memory = new NpcMemory();
        var home = new Vec3i(10, 65, -5);

        memory.RememberPlace("home", home);

        Assert.Equal(home, memory.Place("home"));
        Assert.Contains("home", memory.Places);
        Assert.True(memory.KnownPlaces.ContainsKey("home"));
    }

    [Fact]
    public void NamedPlaces_UnknownPlace_ReturnsNull()
    {
        var memory = new NpcMemory();
        Assert.Null(memory.Place("unknown"));
    }

    [Fact]
    public void NamedPlaces_ForgetPlace_RemovesPlace()
    {
        var memory = new NpcMemory();
        memory.RememberPlace("chest", new Vec3i(1, 2, 3));

        Assert.True(memory.ForgetPlace("chest"));
        Assert.Null(memory.Place("chest"));
        Assert.False(memory.ForgetPlace("chest"));
    }

    [Fact]
    public void NamedPlaces_MaxPlacesLimit_Enforces64Places()
    {
        var memory = new NpcMemory();

        for (int i = 0; i < NpcMemory.MaxPlaces; i++)
        {
            memory.RememberPlace($"place_{i}", new Vec3i(i, 64, i));
        }

        Assert.Equal(64, memory.Places.Count);

        // 65th new place throws exception
        var ex = Assert.Throws<InvalidOperationException>(() => memory.RememberPlace("place_overflow", Vec3i.Zero));
        Assert.Contains("Known places limit exceeded", ex.Message);

        // Updating an existing place among the 64 succeeds
        memory.RememberPlace("place_0", new Vec3i(100, 100, 100));
        Assert.Equal(new Vec3i(100, 100, 100), memory.Place("place_0"));
    }

    [Fact]
    public void NamedPlaces_Validation()
    {
        var memory = new NpcMemory();

        Assert.Throws<ArgumentException>(() => memory.RememberPlace("", Vec3i.Zero));
        Assert.Throws<ArgumentException>(() => memory.RememberPlace("   ", Vec3i.Zero));

        string longName = new string('p', NpcMemory.MaxPlaceNameLength + 1);
        Assert.Throws<ArgumentException>(() => memory.RememberPlace(longName, Vec3i.Zero));
    }

    #endregion

    #region Event Log & Prompt Rendering Tests

    [Fact]
    public void EventLog_AddTypedEvents_StoredInChronologicalOrder()
    {
        var memory = new NpcMemory();

        memory.RecordSpeechHeard("Adrian", "thanks!", isPlayer: true, timestamp: 10.0);
        memory.RecordSpeechSpoke("I will gather wood.", timestamp: 20.0);
        memory.RecordInstruction("Adrian", "Build a stone wall", timestamp: 30.0);
        memory.RecordTaskFinished("Chop trees", true, "got 10 logs", timestamp: 40.0);
        memory.RecordBrainError("attempt to index nil value", timestamp: 50.0);
        memory.RecordAttacked("Zombie", 12.0f, timestamp: 60.0);
        memory.RecordCustom("Found iron ore", "discovery", timestamp: 70.0);

        Assert.Equal(7, memory.EventCount);
        var events = memory.GetAllEvents();

        Assert.IsType<SpeechHeardEvent>(events[0]);
        Assert.IsType<SpeechSpokeEvent>(events[1]);
        Assert.IsType<InstructionReceivedEvent>(events[2]);
        Assert.IsType<TaskFinishedEvent>(events[3]);
        Assert.IsType<BrainErrorEvent>(events[4]);
        Assert.IsType<AttackedEvent>(events[5]);
        Assert.IsType<CustomMemoryEvent>(events[6]);
    }

    [Fact]
    public void EventLog_RingBufferCapacity_EvictsOldestWhenExceeded()
    {
        var memory = new NpcMemory(eventCapacity: 3);

        memory.RecordSpeechSpoke("one", 1.0);
        memory.RecordSpeechSpoke("two", 2.0);
        memory.RecordSpeechSpoke("three", 3.0);
        memory.RecordSpeechSpoke("four", 4.0); // Evicts "one"

        Assert.Equal(3, memory.EventCount);
        var events = memory.GetAllEvents();
        Assert.Equal("Said: \"two\"", events[0].RenderContent());
        Assert.Equal("Said: \"three\"", events[1].RenderContent());
        Assert.Equal("Said: \"four\"", events[2].RenderContent());
    }

    [Fact]
    public void EventLog_GetRecentEvents_ReturnsChronologicalWithNewestLast()
    {
        var memory = new NpcMemory(eventCapacity: 10);

        for (int i = 1; i <= 8; i++)
        {
            memory.RecordSpeechSpoke($"line {i}", i);
        }

        var recent = memory.GetRecentEvents(3);
        Assert.Equal(3, recent.Count);
        Assert.Equal("Said: \"line 6\"", recent[0].RenderContent());
        Assert.Equal("Said: \"line 7\"", recent[1].RenderContent());
        Assert.Equal("Said: \"line 8\"", recent[2].RenderContent());
    }

    [Fact]
    public void EventLog_RenderEventsForPrompt_FormatsCorrectlyMatchingSpec()
    {
        var memory = new NpcMemory();

        // Simulate event timeline:
        // current time = 600s (10 min)
        // evt1: player said thanks at 585s (15s ago -> under 1 min -> no suffix)
        // evt2: brain error at 300s (300s ago -> 5 min ago)
        memory.RecordSpeechHeard("Player Adrian", "thanks!", isPlayer: true, timestamp: 585.0);
        memory.RecordBrainError("attempt to index nil value", timestamp: 300.0);

        string promptText = memory.RenderEventsForPrompt(currentTime: 600.0, maxCount: 15);

        // Verification of spec from LLM_INTEGRATION.md §2.2:
        // - Player Adrian said: "thanks!"
        // - Task failed: brain_error "attempt to index nil value" (5 min ago)
        var lines = promptText.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("- Player Adrian said: \"thanks!\"", lines[0]);
        Assert.Equal("- Task failed: brain_error \"attempt to index nil value\" (5 min ago)", lines[1]);
    }

    #endregion

    #region Serialization Round-Trip Tests

    [Fact]
    public void Serialization_CompleteRoundTrip_PreservesAllState()
    {
        var memory = new NpcMemory(eventCapacity: 20);

        // 1. Populate KV store
        memory.Set("is_working", true);
        memory.Set("delivered", 5);
        memory.Set("speed", 1.75);
        memory.Set("instructor_name", "Adrian");
        memory.Set("chest_pos", new Vec3i(15, 64, -20));

        var flatDict = new Dictionary<string, object>
        {
            ["task"] = "mine",
            ["yield"] = 12
        };
        memory.Set("meta", flatDict);

        var flatList = new List<object> { "iron_ore", 4, true };
        memory.Set("items", flatList);

        // 2. Populate Places
        memory.RememberPlace("home", new Vec3i(0, 65, 0));
        memory.RememberPlace("player_chest", new Vec3i(15, 64, -20));
        memory.RememberPlace("wheat_field", new Vec3i(-30, 64, 45));

        // 3. Populate Events
        memory.RecordSpeechHeard("Adrian", "thanks!", isPlayer: true, timestamp: 100.0);
        memory.RecordSpeechSpoke("On my way", timestamp: 110.0);
        memory.RecordInstruction("Adrian", "Chop trees", timestamp: 120.0);
        memory.RecordTaskFinished("Chop trees", true, "got 8 logs", timestamp: 130.0);
        memory.RecordBrainError("out of bounds", timestamp: 140.0);
        memory.RecordAttacked("Spider", 8.0f, timestamp: 150.0);
        memory.RecordCustom("Level up", "progression", timestamp: 160.0);

        // 4. Serialize to JSON
        string json = memory.ToJson(indented: true);
        Assert.False(string.IsNullOrWhiteSpace(json));

        // 5. Deserialize back from JSON
        var restored = NpcMemory.FromJson(json);

        // 6. Verify KV store
        Assert.Equal(memory.KeyCount, restored.KeyCount);
        Assert.True(restored.Get<bool>("is_working", false));
        Assert.Equal(5, restored.Get<int>("delivered", 0));
        Assert.Equal(1.75, restored.Get<double>("speed", 0.0), 2);
        Assert.Equal("Adrian", restored.Get<string>("instructor_name", ""));
        Assert.Equal(new Vec3i(15, 64, -20), restored.Get<Vec3i>("chest_pos", Vec3i.Zero));

        var restoredDict = restored.Get("meta") as IDictionary<string, object>;
        Assert.NotNull(restoredDict);
        Assert.Equal("mine", restoredDict!["task"]);

        var restoredList = restored.Get("items") as IList<object>;
        Assert.NotNull(restoredList);
        Assert.Equal("iron_ore", restoredList![0]);

        // 7. Verify Named Places
        Assert.Equal(3, restored.Places.Count);
        Assert.Equal(new Vec3i(0, 65, 0), restored.Place("home"));
        Assert.Equal(new Vec3i(15, 64, -20), restored.Place("player_chest"));
        Assert.Equal(new Vec3i(-30, 64, 45), restored.Place("wheat_field"));

        // 8. Verify Events
        Assert.Equal(7, restored.EventCount);
        var restoredEvents = restored.GetAllEvents();
        Assert.Equal(7, restoredEvents.Count);
        Assert.Equal("- Player Adrian said: \"thanks!\"", restoredEvents[0].Render());
        Assert.Equal("- Said: \"On my way\"", restoredEvents[1].Render());
        Assert.Equal("- Instructed by Adrian: \"Chop trees\"", restoredEvents[2].Render());
        Assert.Equal("- Task completed: Chop trees (got 8 logs)", restoredEvents[3].Render());
        Assert.Equal("- Task failed: brain_error \"out of bounds\"", restoredEvents[4].Render());
        Assert.Equal("- Attacked: took 8 damage from Spider", restoredEvents[5].Render());
        Assert.Equal("- Level up", restoredEvents[6].Render());

        // 9. Verify restored memory accepts new modifications
        restored.Set("new_key", 100);
        Assert.Equal(100, restored.Get<int>("new_key", 0));
        restored.RememberPlace("mine", new Vec3i(5, 40, 5));
        Assert.Equal(new Vec3i(5, 40, 5), restored.Place("mine"));
        restored.RecordSpeechSpoke("Still alive", 200.0);
        Assert.Equal(8, restored.EventCount);
    }

    [Fact]
    public void Serialization_InvalidJson_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => NpcMemory.FromJson(""));
        Assert.Throws<ArgumentException>(() => NpcMemory.FromJson("   "));
    }

    #endregion

    #region NpcState Integration Tests

    [Fact]
    public void NpcState_InitializesMemoryWithHomePlace()
    {
        var home = new Vec3(12.5f, 65.0f, -4.2f);
        var npc = new NpcState(new EntityId(1), "Bob", home);

        Assert.NotNull(npc.Memory);
        var homePlace = npc.Memory.Place("home");
        Assert.NotNull(homePlace);
        Assert.Equal(new Vec3i(12, 65, -5), homePlace!.Value);
    }

    [Fact]
    public void NpcState_TakeDamageWithSource_RecordsAttackedEventInMemory()
    {
        var npc = new NpcState(new EntityId(1), "Bob", Vec3.Zero);
        npc.TakeDamage(15f, source: "Skeleton");

        Assert.Equal(85f, npc.Health);
        Assert.Equal(1, npc.Memory.EventCount);

        var evt = npc.Memory.GetAllEvents()[0] as AttackedEvent;
        Assert.NotNull(evt);
        Assert.Equal("Skeleton", evt!.Attacker);
        Assert.Equal(15f, evt.Damage);
    }

    #endregion
}
