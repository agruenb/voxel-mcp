namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Perception;
using Xunit;

public class LuaApiRegistryTests
{
    private static readonly HashSet<string> ExpectedSpecFunctions = new(StringComparer.Ordinal)
    {
        // 3.1 npc self (9)
        "npc.id",
        "npc.name",
        "npc.pos",
        "npc.home",
        "npc.health",
        "npc.inventory",
        "npc.count",
        "npc.free_slots",
        "npc.instructor",

        // 3.2 npc actions (11)
        "npc.go_to",
        "npc.go_near",
        "npc.follow",
        "npc.mine",
        "npc.place",
        "npc.pick_up",
        "npc.drop",
        "npc.give",
        "npc.deposit",
        "npc.withdraw",
        "npc.wait",

        // 3.3 npc instantaneous / control (6)
        "npc.say",
        "npc.look_at",
        "npc.done",
        "npc.restart",
        "npc.interrupt",
        "log",

        // 3.4 world perception (10)
        "world.block",
        "world.find_block",
        "world.find_blocks",
        "world.find_entity",
        "world.find_entities",
        "world.entity",
        "world.is_walkable",
        "world.surface",
        "world.time",
        "world.is_day",

        // 3.5 memory (5)
        "memory.get",
        "memory.set",
        "memory.place",
        "memory.remember_place",
        "memory.places",

        // 3.6 utilities (3)
        "vec",
        "random",
        "dist"
    };

    private static readonly HashSet<string> ExpectedBlockingActions = new(StringComparer.Ordinal)
    {
        "npc.go_to",
        "npc.go_near",
        "npc.follow",
        "npc.mine",
        "npc.place",
        "npc.pick_up",
        "npc.drop",
        "npc.give",
        "npc.deposit",
        "npc.withdraw",
        "npc.wait"
    };

    [Fact]
    public void SpecCompleteness_EveryFunctionInSpecIsRegisteredBidirectionally()
    {
        var registry = LuaApiRegistry.Default;
        var entries = registry.GetAllEntries();
        var registeredNames = entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);

        // 1. Every spec function must be registered
        foreach (var specFunc in ExpectedSpecFunctions)
        {
            Assert.True(registeredNames.Contains(specFunc), $"Spec function '{specFunc}' was not found in LuaApiRegistry.");
            var entry = registry.GetEntry(specFunc);
            Assert.NotNull(entry);
            Assert.False(string.IsNullOrWhiteSpace(entry.Description), $"Function '{specFunc}' missing description.");

            bool expectedBlocking = ExpectedBlockingActions.Contains(specFunc);
            Assert.Equal(expectedBlocking, entry.IsBlocking);
            Assert.Equal(!expectedBlocking, entry.AllowedInHandler);
        }

        // 2. Registry must not contain unexpected functions
        foreach (var regFunc in registeredNames)
        {
            Assert.True(ExpectedSpecFunctions.Contains(regFunc), $"Registry contains unknown function '{regFunc}' not in spec.");
        }

        Assert.Equal(ExpectedSpecFunctions.Count, entries.Count);
        Assert.Equal(44, entries.Count);
    }

    [Fact]
    public void GeneratedReferenceSnapshot_ProducesValidMarkdownDocumentation()
    {
        var registry = LuaApiRegistry.Default;
        string md = registry.GenerateMarkdownReference();

        Assert.Contains("# Lua Brain API Reference", md);
        Assert.Contains("### 3.1 `npc` — self (non-blocking queries)", md);
        Assert.Contains("### 3.2 `npc` — blocking actions (only inside `main`)", md);
        Assert.Contains("### 3.3 `npc` — instantaneous / control", md);
        Assert.Contains("### 3.4 `world` — perception (non-blocking, limited by perception radius)", md);
        Assert.Contains("### 3.5 `memory` — persistent per-NPC storage", md);
        Assert.Contains("### 3.6 Utilities", md);

        // Every spec function should be mentioned in the markdown
        foreach (var specFunc in ExpectedSpecFunctions)
        {
            Assert.Contains($"`{specFunc}", md);
        }
    }

    [Fact]
    public void GeneratedReferenceSnapshot_ProducesValidPlaintextDocumentation()
    {
        var registry = LuaApiRegistry.Default;
        string txt = registry.GeneratePlaintextReference();

        foreach (var specFunc in ExpectedSpecFunctions)
        {
            Assert.Contains(specFunc, txt);
        }

        foreach (var blocking in ExpectedBlockingActions)
        {
            Assert.Contains($"{blocking}", txt);
            Assert.Contains("[BLOCKING]", txt);
        }
    }

    private static (Script Script, NpcBrainContext Context, NpcState Npc) CreateTestEnvironment()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript(seed: 42);

        var npc = new NpcState(new EntityId(42), "MinerBob", new Vec3(10f, 64f, 15f), home: new Vec3(0f, 64f, 0f));
        npc.Inventory.AddItem("stone", 12, out _);
        npc.Inventory.AddItem("oak_log", 5, out _);
        npc.InstructorId = new EntityId(100);

        var context = new NpcBrainContext(npc);
        LuaApiRegistry.Default.RegisterIntoScript(script, context);

        return (script, context, npc);
    }

    [Fact]
    public void PosMetatable_SupportsAllVectorOperations()
    {
        var (script, _, _) = CreateTestEnvironment();

        // vec creation
        var res = script.DoString(@"
            local p1 = vec(1, 2, 3)
            local p2 = vec(4, 6, 8)

            local p3 = p1 + p2
            local p4 = p2 - p1
            local eq_same = (p1 == vec(1, 2, 3))
            local eq_diff = (p1 == p2)
            local d = p1:dist(vec(1, 2, 7))
            local off = p1:offset(10, -1, 5)
            local s = tostring(p1)
            local s_method = p1:tostring()
            local d_global = dist(p1, vec(1, 2, 7))

            return p3.x, p3.y, p3.z, p4.x, p4.y, p4.z, eq_same, eq_diff, d, off.x, off.y, off.z, s, s_method, d_global
        ");

        var tuple = res.Tuple;
        Assert.Equal(5, tuple[0].Number);
        Assert.Equal(8, tuple[1].Number);
        Assert.Equal(11, tuple[2].Number);

        Assert.Equal(3, tuple[3].Number);
        Assert.Equal(4, tuple[4].Number);
        Assert.Equal(5, tuple[5].Number);

        Assert.True(tuple[6].Boolean);
        Assert.False(tuple[7].Boolean);
        Assert.Equal(4.0, tuple[8].Number);

        Assert.Equal(11, tuple[9].Number);
        Assert.Equal(1, tuple[10].Number);
        Assert.Equal(8, tuple[11].Number);

        Assert.Equal("pos(1, 2, 3)", tuple[12].String);
        Assert.Equal("pos(1, 2, 3)", tuple[13].String);
        Assert.Equal(4.0, tuple[14].Number);
    }

    [Fact]
    public void NpcQueries_ReturnAccurateNpcState()
    {
        var (script, _, npc) = CreateTestEnvironment();

        var res = script.DoString(@"
            local id = npc.id()
            local name = npc.name()
            local pos = npc.pos()
            local home = npc.home()
            local hp = npc.health()
            local inv = npc.inventory()
            local stoneCount = npc.count('stone')
            local free = npc.free_slots()
            local instructor = npc.instructor()

            return id, name, pos.x, pos.y, pos.z, home.x, home.y, home.z, hp, inv['stone'], stoneCount, free, instructor
        ");

        var tuple = res.Tuple;
        Assert.Equal(42, tuple[0].Number);
        Assert.Equal("MinerBob", tuple[1].String);

        Assert.Equal(10, tuple[2].Number);
        Assert.Equal(64, tuple[3].Number);
        Assert.Equal(15, tuple[4].Number);

        Assert.Equal(0, tuple[5].Number);
        Assert.Equal(64, tuple[6].Number);
        Assert.Equal(0, tuple[7].Number);

        Assert.Equal(100, tuple[8].Number);
        Assert.Equal(12, tuple[9].Number);
        Assert.Equal(12, tuple[10].Number);
        Assert.Equal(npc.Inventory.FreeSlots, tuple[11].Number);
        Assert.Equal(100, tuple[12].Number);
    }

    [Fact]
    public void NpcControl_InstantaneousActionsOperateCorrectly()
    {
        var (script, context, npc) = CreateTestEnvironment();
        string? logged = null;
        string? said = null;

        var contextWithCallbacks = new NpcBrainContext(
            npc,
            logCallback: msg => logged = msg,
            sayCallback: msg => said = msg);

        LuaApiRegistry.Default.RegisterIntoScript(script, contextWithCallbacks);

        // 1. log
        script.DoString("log('Hello', 'from', 'Lua', 42)");
        Assert.Equal("Hello from Lua 42", logged);

        // 2. say
        script.DoString("npc.say('Greetings adventurer!')");
        Assert.Equal("Greetings adventurer!", said);

        // 3. look_at
        script.DoString("npc.look_at(vec(20, 64, 15))");
        Assert.True(npc.Facing != 0f);

        // 4. done
        script.DoString("npc.done('Task complete')");
        Assert.True(contextWithCallbacks.IsDone);
        Assert.Equal("Task complete", contextWithCallbacks.DoneSummary);

        // 5. restart
        script.DoString("npc.restart()");
        Assert.True(contextWithCallbacks.RestartRequested);
    }

    [Fact]
    public void NpcControl_SayRateLimitsAndTruncates()
    {
        var (script, context, npc) = CreateTestEnvironment();
        int sayCount = 0;
        string? lastSaid = null;

        var customContext = new NpcBrainContext(
            npc,
            sayCallback: s => { sayCount++; lastSaid = s; });

        LuaApiRegistry.Default.RegisterIntoScript(script, customContext);

        // 1. First say succeeds
        customContext.CurrentTime = 10.0f;
        script.DoString("npc.say('First line')");
        Assert.Equal(1, sayCount);
        Assert.Equal("First line", lastSaid);

        // 2. Second say within 2 seconds is dropped
        customContext.CurrentTime = 11.0f;
        script.DoString("npc.say('Dropped line')");
        Assert.Equal(1, sayCount);
        Assert.Equal("First line", lastSaid);

        // 3. Say after 2 seconds succeeds
        customContext.CurrentTime = 12.1f;
        script.DoString("npc.say('Second line')");
        Assert.Equal(2, sayCount);
        Assert.Equal("Second line", lastSaid);

        // 4. Truncation to 200 chars
        customContext.CurrentTime = 15.0f;
        string longStr = new string('A', 300);
        script.DoString($"npc.say('{longStr}')");
        Assert.Equal(3, sayCount);
        Assert.Equal(200, lastSaid?.Length);
    }

    private sealed class MockPerception : INpcPerception
    {
        public string? GetBlock(Vec3i pos) => pos == new Vec3i(1, 2, 3) ? "diamond_ore" : null;
        public Vec3i? FindBlock(string blockId, int? radius = null) => blockId == "oak_log" ? new Vec3i(5, 64, 5) : null;
        public IReadOnlyList<Vec3i> FindBlocks(string blockId, int? radius = null, int max = 64) =>
            blockId == "oak_log" ? new[] { new Vec3i(5, 64, 5), new Vec3i(8, 64, 8) } : Array.Empty<Vec3i>();

        public EntityId? FindEntity(EntityKind kind, float? radius = null) => kind == EntityKind.Player ? new EntityId(1) : null;
        public EntityId? FindEntity(string kindString, float? radius = null) => kindString == "player" ? new EntityId(1) : null;
        public IReadOnlyList<EntityId> FindEntities(EntityKind kind, float? radius = null) =>
            kind == EntityKind.Player ? new[] { new EntityId(1) } : Array.Empty<EntityId>();
        public IReadOnlyList<EntityId> FindEntities(string kindString, float? radius = null) =>
            kindString == "player" ? new[] { new EntityId(1) } : Array.Empty<EntityId>();

        public PerceivedEntity? GetEntity(EntityId id) => id.Value == 1
            ? new PerceivedEntity(new EntityId(1), EntityKind.Player, "Alice", new Vec3(10f, 65f, 10f), 100f)
            : null;

        public bool IsWalkable(Vec3i pos) => pos.Y == 64;
        public Vec3i? GetSurface(int x, int z) => new Vec3i(x, 64, z);
        public float GetTime() => 14.5f;
        public bool IsDay() => true;
    }

    [Fact]
    public void WorldPerception_BindsAndQueriesAccurately()
    {
        var (script, _, npc) = CreateTestEnvironment();
        npc.Perception = new MockPerception();

        var res = script.DoString(@"
            local b = world.block(vec(1, 2, 3))
            local fb = world.find_block('oak_log', 24)
            local fbs = world.find_blocks('oak_log', 24, 10)
            local fe = world.find_entity('player', 32)
            local fes = world.find_entities('player', 32)
            local ent = world.entity(1)
            local walkable = world.is_walkable(vec(0, 64, 0))
            local surface = world.surface(5, 5)
            local time = world.time()
            local isDay = world.is_day()

            return b, fb.x, #fbs, fe, #fes, ent.name, ent.pos.y, walkable, surface.y, time, isDay
        ");

        var tuple = res.Tuple;
        Assert.Equal("diamond_ore", tuple[0].String);
        Assert.Equal(5, tuple[1].Number);
        Assert.Equal(2, tuple[2].Number);
        Assert.Equal(1, tuple[3].Number);
        Assert.Equal(1, tuple[4].Number);
        Assert.Equal("Alice", tuple[5].String);
        Assert.Equal(65, tuple[6].Number);
        Assert.True(tuple[7].Boolean);
        Assert.Equal(64, tuple[8].Number);
        Assert.Equal(14.5, tuple[9].Number);
        Assert.True(tuple[10].Boolean);
    }

    [Fact]
    public void MemoryStorage_PerformsFullRoundtripWithLimits()
    {
        var (script, _, _) = CreateTestEnvironment();

        script.DoString(@"
            memory.set('target_count', 10)
            memory.set('greeting', 'hello')
            memory.set('active', true)
            memory.set('chest_pos', vec(10, 20, 30))
            memory.remember_place('player_chest', vec(15, 65, 15))
        ");

        var res = script.DoString(@"
            local tc = memory.get('target_count')
            local gr = memory.get('greeting')
            local act = memory.get('active')
            local cp = memory.get('chest_pos')
            local def = memory.get('unknown_key', 'fallback')
            local chestPlace = memory.place('player_chest')
            local places = memory.places()

            return tc, gr, act, cp.x, cp.y, cp.z, def, chestPlace.x, #places
        ");

        var tuple = res.Tuple;
        Assert.Equal(10, tuple[0].Number);
        Assert.Equal("hello", tuple[1].String);
        Assert.True(tuple[2].Boolean);
        Assert.Equal(10, tuple[3].Number);
        Assert.Equal(20, tuple[4].Number);
        Assert.Equal(30, tuple[5].Number);
        Assert.Equal("fallback", tuple[6].String);
        Assert.Equal(15, tuple[7].Number);
        Assert.Equal(2, tuple[8].Number); // 'home' and 'player_chest'

        // Delete key by setting nil
        script.DoString("memory.set('greeting', nil)");
        var deleted = script.DoString("return memory.get('greeting', 'deleted')").String;
        Assert.Equal("deleted", deleted);
    }

    [Fact]
    public void BlockingActions_YieldCoroutineAndResumeWithResults()
    {
        var (script, context, _) = CreateTestEnvironment();

        script.DoString(@"
            function main()
                local ok, reason = npc.go_to(vec(5, 64, 5), {tolerance = 2})
                return ok, reason
            end
        ");

        var coro = script.CreateCoroutine(script.Globals.Get("main"));

        // First resume initiates action and yields
        var yieldRes = coro.Coroutine.Resume();
        Assert.Equal(CoroutineState.Suspended, coro.Coroutine.State);
        Assert.NotNull(context.LastActionRequest);
        Assert.Equal("go_to", context.LastActionRequest.ActionName);

        // Host finishes action and resumes coroutine with success (true, nil)
        var completeRes = coro.Coroutine.Resume(DynValue.True, DynValue.Nil);
        Assert.Equal(CoroutineState.Dead, coro.Coroutine.State);
        Assert.True(completeRes.Tuple[0].Boolean);
        Assert.True(completeRes.Tuple[1].IsNil());
    }

    [Fact]
    public void BlockingActions_ThrowWhenCalledInsideHandler()
    {
        var (script, context, _) = CreateTestEnvironment();

        context.IsInsideHandler = true;

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("npc.go_to(vec(0, 0, 0))");
        });

        Assert.Contains("blocking function 'npc.go_to' cannot be called from an event handler", ex.Message);
    }

    [Fact]
    public void ApiTables_AreProtectedAgainstModification()
    {
        var (script, _, _) = CreateTestEnvironment();

        Assert.Equal("protected", script.DoString("return getmetatable(npc)").String);
        Assert.Equal("protected", script.DoString("return getmetatable(world)").String);
        Assert.Equal("protected", script.DoString("return getmetatable(memory)").String);

        var exNpc = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("setmetatable(npc, {})");
        });
        Assert.Contains("cannot change a protected metatable", exNpc.Message);

        var exWorld = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("setmetatable(world, {})");
        });
        Assert.Contains("cannot change a protected metatable", exWorld.Message);

        var exMemory = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("setmetatable(memory, {})");
        });
        Assert.Contains("cannot change a protected metatable", exMemory.Message);
    }

    [Fact]
    public void Vec3Alias_CreatesValidPosIdenticalToVec()
    {
        var (script, _, _) = CreateTestEnvironment();

        var res = script.DoString(@"
            local p1 = vec(1, 2, 3)
            local p2 = vec3(1, 2, 3)
            return p1 == p2, p2.x, p2.y, p2.z
        ");

        Assert.True(res.Tuple[0].Boolean);
        Assert.Equal(1, res.Tuple[1].Number);
        Assert.Equal(2, res.Tuple[2].Number);
        Assert.Equal(3, res.Tuple[3].Number);
    }

    [Fact]
    public void WorldGetBlockAlias_MatchesWorldBlock()
    {
        var (script, _, _) = CreateTestEnvironment();

        var res = script.DoString(@"
            local b1 = world.block(vec(0, 63, 0))
            local b2 = world.get_block(vec3(0, 63, 0))
            return b1, b2, b1 == b2
        ");

        Assert.Equal(res.Tuple[0].String, res.Tuple[1].String);
        Assert.True(res.Tuple[2].Boolean);
    }
}
