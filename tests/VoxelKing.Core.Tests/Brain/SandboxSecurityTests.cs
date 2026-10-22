namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Linq;
using MoonSharp.Interpreter;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Actions;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Voxel;
using Xunit;

/// <summary>
/// Comprehensive sandbox security test suite for Lua brains (F-304, ARCHITECTURE §5.4, LUA_BRAIN_API §4).
/// Tests attempting escapes: load, string.dump, metatable tricks on _G/API tables, debug,
/// huge string allocation, deep recursion, infinite loops in top-level/handlers/main, and yield from handlers.
/// Verifies that all attempts fail safely without crashing or hanging the host.
/// </summary>
public class SandboxSecurityTests
{
    private static (NpcState npc, BrainHost host, EventBus eventBus, EntityRegistry registry) CreateTestHost(
        BrainLimitsConfig? limits = null,
        IActionFactory? actionFactory = null)
    {
        var registry = new EntityRegistry();
        var eventBus = new EventBus();
        var memory = new NpcMemory(limits);
        var npc = new NpcState(
            new EntityId(1),
            "SecBot",
            new Vec3(10f, 64f, 10f),
            home: new Vec3(10f, 64f, 10f),
            memory: memory);

        registry.Register(npc);

        var host = new BrainHost(
            npc,
            limitsConfig: limits,
            entityRegistry: registry,
            eventBus: eventBus,
            actionFactory: actionFactory);

        npc.BrainHost = host;
        return (npc, host, eventBus, registry);
    }

    #region 1. Escape Attempts: load / dynamic code execution

    [Theory]
    [InlineData("load")]
    [InlineData("loadstring")]
    [InlineData("loadfile")]
    [InlineData("dofile")]
    [InlineData("require")]
    public void LoadEscapes_CallingForbiddenFunctions_ThrowsUnknownGlobal(string funcName)
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString($"{funcName}('return 1')");
        });
        Assert.Contains($"unknown global '{funcName}'", ex.Message);
    }

    [Fact]
    public void LoadEscapes_PackageNamespace_IsAbsent()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("local p = package.loaded");
        });
        Assert.Contains("unknown global 'package'", ex.Message);
    }

    [Theory]
    [InlineData("_G['load']('return 1')")]
    [InlineData("_G.load('return 1')")]
    [InlineData("_ENV['load']('return 1')")]
    [InlineData("_ENV.load('return 1')")]
    [InlineData("pcall(load, 'return 1')")]
    public void LoadEscapes_IndirectAccessViaGOrEnv_FailsSafely(string code)
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString(code);
        });
        Assert.Contains("unknown global 'load'", ex.Message);
    }

    [Fact]
    public void LoadEscapes_InsideBrainHost_FailsSafelyAndFaults()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            function main()
                load('npc.say(""escaped"")')()
            end
        ";

        bool loaded = host.LoadScript(script);
        Assert.True(loaded);

        host.Tick(0.1f);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("unknown global 'load'", host.LastError);

        var err = npc.Memory.GetAllEvents().OfType<BrainErrorEvent>().FirstOrDefault();
        Assert.NotNull(err);
        Assert.Contains("unknown global 'load'", err.Error);
    }

    #endregion

    #region 2. Escape Attempts: string.dump

    [Fact]
    public void StringDump_DirectAndMethodCall_ThrowsAttemptToCallNil()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var exDirect = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("string.dump(function() end)");
        });
        Assert.Contains("attempt to call a nil value", exDirect.Message);

        var exMethod = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("('abc'):dump()");
        });
        Assert.Contains("attempt to call a nil value", exMethod.Message);
    }

    [Fact]
    public void StringDump_ViaStringMetatable_IsNil()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var res = script.DoString("return getmetatable('').dump == nil and getmetatable('').__index.dump == nil");
        Assert.True(res.Boolean);
    }

    [Fact]
    public void StringDump_InsideBrainHost_FaultsSafely()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            function main()
                local bytecode = string.dump(main)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("attempt to call a nil value", host.LastError);
    }

    #endregion

    #region 3. Escape Attempts: Metatable Tricks

    [Fact]
    public void MetatableTricks_GlobalEnvironment_IsProtectedAgainstModification()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        Assert.Equal("protected", script.DoString("return getmetatable(_G)").String);

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("setmetatable(_G, {})");
        });
        Assert.Contains("cannot change a protected metatable", ex.Message);
    }

    [Theory]
    [InlineData("rawset(_G, '__index', nil)")]
    [InlineData("rawget(_G, 'load')")]
    public void MetatableTricks_RawsetAndRawget_AreForbidden(string code)
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString(code);
        });
        Assert.True(ex.Message.Contains("unknown global 'rawset'") || ex.Message.Contains("unknown global 'rawget'"));
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("world")]
    [InlineData("memory")]
    public void MetatableTricks_ApiTables_AreProtectedAgainstModification(string tableName)
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = $@"
            function main()
                setmetatable({tableName}, {{}})
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("cannot change a protected metatable", host.LastError);
    }

    [Fact]
    public void MetatableTricks_ApiTables_GetMetatableReturnsProtected()
    {
        var (_, host, _, _) = CreateTestHost();
        string script = @"
            function main()
                if getmetatable(npc) ~= 'protected' or
                   getmetatable(world) ~= 'protected' or
                   getmetatable(memory) ~= 'protected' then
                    error('metatable unprotected')
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Completed, host.Status);
    }

    [Fact]
    public void MetatableTricks_VectorPosMetatable_IsProtectedAgainstTampering()
    {
        var (_, host, _, _) = CreateTestHost();
        string script = @"
            function main()
                local p = vec(1, 2, 3)
                if getmetatable(p) ~= 'protected' then
                    error('pos metatable unprotected')
                end
                setmetatable(p, {})
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("cannot change a protected metatable", host.LastError);
    }

    [Theory]
    [InlineData("math")]
    [InlineData("string")]
    [InlineData("table")]
    public void MetatableTricks_StandardLibraries_AreProtectedAgainstModification(string libName)
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        Assert.Equal("protected", script.DoString($"return getmetatable({libName})").String);

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString($"setmetatable({libName}, {{}})");
        });
        Assert.Contains("cannot change a protected metatable", ex.Message);
    }

    #endregion

    #region 4. Escape Attempts: debug library

    [Theory]
    [InlineData("local d = debug")]
    [InlineData("debug.getinfo(1)")]
    [InlineData("debug.getupvalue(function() end, 1)")]
    [InlineData("debug.setupvalue(function() end, 1, 2)")]
    [InlineData("debug.setmetatable({}, {})")]
    [InlineData("debug.getmetatable({})")]
    [InlineData("debug.sethook(function() end, 'c')")]
    [InlineData("debug.traceback()")]
    public void DebugEscapes_AllDebugFunctions_ThrowUnknownGlobal(string code)
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString(code);
        });
        Assert.Contains("unknown global 'debug'", ex.Message);
    }

    [Fact]
    public void DebugEscapes_InsideBrainHost_FaultsSafely()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            function main()
                local info = debug.getinfo(1)
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains("unknown global 'debug'", host.LastError);
    }

    #endregion

    #region 5. Resource Exhaustion: Huge String Allocation

    [Fact]
    public void HugeStringAllocation_InTopLevel_FailsValidationWithoutHanging()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            local s = 'voxel'
            while true do
                s = s .. s
            end

            function main()
            end
        ";

        bool loaded = host.LoadScript(script);
        Assert.False(loaded);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
        Assert.True(host.LastError == "top-level instruction budget exceeded" || host.LastError.StartsWith("top-level execution error"));
    }

    [Fact]
    public void HugeStringAllocation_InMainLoop_TerminatesAsBusyLoopWithoutHanging()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerTick = 100,
            MaxConsecutiveForcedYields = 5
        };

        var (npc, host, _, _) = CreateTestHost(limits);
        string script = @"
            function main()
                local s = 'a'
                while true do
                    s = s .. 'b'
                end
            end
        ";

        host.LoadScript(script);

        // Advance ticks: AutoYieldCounter forces yields, leading to busy loop termination
        for (int i = 0; i < 10 && host.Status == BrainStatus.Running; i++)
        {
            host.Tick(0.1f);
        }

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("busy loop", host.LastError);
    }

    [Fact]
    public void HugeStringAllocation_InEventHandler_FailsBudgetWithoutHanging()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerHandler = 200
        };

        var (npc, host, _, _) = CreateTestHost(limits);
        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker, text)
                local s = 'boom'
                while true do
                    s = s .. s
                end
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        host.OnHeard(new EntityId(2), "allocate");
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("handler budget exceeded", host.LastError);
    }

    #endregion

    #region 6. Resource Exhaustion: Deep Recursion

    [Fact]
    public void DeepRecursion_DirectInTopLevel_FailsSafelyWithoutCrashingHost()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            local function recurse()
                return recurse()
            end
            recurse()

            function main() end
        ";

        bool loaded = host.LoadScript(script);
        Assert.False(loaded);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
    }

    [Fact]
    public void DeepRecursion_MutualInTopLevel_FailsSafelyWithoutCrashingHost()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            local a, b
            a = function() return b() end
            b = function() return a() end
            a()

            function main() end
        ";

        bool loaded = host.LoadScript(script);
        Assert.False(loaded);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
    }

    [Fact]
    public void DeepRecursion_DirectInMain_FaultsSafelyWithoutCrashingHost()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            local function recurse()
                return 1 + recurse()
            end

            function main()
                recurse()
            end
        ";

        host.LoadScript(script);

        // Advance ticks: either stack overflow or busy loop will fault it safely
        for (int i = 0; i < 60 && host.Status == BrainStatus.Running; i++)
        {
            host.Tick(0.1f);
        }

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);

        var err = npc.Memory.GetAllEvents().OfType<BrainErrorEvent>().FirstOrDefault();
        Assert.NotNull(err);
    }

    [Fact]
    public void DeepRecursion_MutualInMain_FaultsSafelyWithoutCrashingHost()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            local a, b
            a = function() return 1 + b() end
            b = function() return 1 + a() end

            function main()
                a()
            end
        ";

        host.LoadScript(script);

        for (int i = 0; i < 60 && host.Status == BrainStatus.Running; i++)
        {
            host.Tick(0.1f);
        }

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
    }

    [Fact]
    public void DeepRecursion_InEventHandler_FaultsSafelyWithoutCrashingHost()
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            local function recurse()
                return recurse()
            end

            function on_attacked(attacker, damage)
                recurse()
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        host.OnAttacked(new EntityId(99), 10f);
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.NotNull(host.LastError);
    }

    #endregion

    #region 7. Resource Exhaustion: Infinite Loops

    [Theory]
    [InlineData("while true do end")]
    [InlineData("repeat until false")]
    [InlineData("for i=1, 100000000 do end")]
    public void InfiniteLoops_InTopLevel_ExceedBudgetAndRejectScriptWithoutHanging(string loopCode)
    {
        var limits = new BrainLimitsConfig
        {
            MaxTopLevelChunkInstructions = 500
        };

        var (npc, host, _, _) = CreateTestHost(limits);
        string script = $@"
            {loopCode}

            function main() end
        ";

        bool loaded = host.LoadScript(script);
        Assert.False(loaded);
        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("top-level instruction budget exceeded", host.LastError);
    }

    [Theory]
    [InlineData("on_heard", "while true do end")]
    [InlineData("on_attacked", "repeat until false")]
    [InlineData("on_player_nearby", "for i=1, 100000000 do end")]
    [InlineData("on_interrupted", "while 1 == 1 do end")]
    public void InfiniteLoops_InEventHandlers_ExceedBudgetAndFaultWithoutHanging(string handlerName, string loopCode)
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerHandler = 300
        };

        var (npc, host, _, _) = CreateTestHost(limits);
        string script = $@"
            function main()
                while true do npc.wait(1) end
            end

            function {handlerName}(...)
                {loopCode}
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        switch (handlerName)
        {
            case "on_heard":
                host.OnHeard(new EntityId(2), "ping");
                break;
            case "on_attacked":
                host.OnAttacked(new EntityId(2), 5f);
                break;
            case "on_player_nearby":
                host.OnPlayerNearby(new EntityId(2));
                break;
            case "on_interrupted":
                host.OnInterrupted("reflex");
                break;
        }

        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("handler budget exceeded", host.LastError);
    }

    [Theory]
    [InlineData("while true do end")]
    [InlineData("repeat until false")]
    [InlineData("local x = 0; while true do x = (x + 1) % 1000 end")]
    public void InfiniteLoops_InMain_TerminatesAsBusyLoopWithoutHanging(string loopCode)
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerTick = 100,
            MaxConsecutiveForcedYields = 10
        };

        var (npc, host, _, _) = CreateTestHost(limits);
        string script = $@"
            function main()
                {loopCode}
            end
        ";

        host.LoadScript(script);

        for (int i = 0; i < 20 && host.Status == BrainStatus.Running; i++)
        {
            host.Tick(0.1f);
        }

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Equal("busy loop", host.LastError);
    }

    #endregion

    #region 8. Control Flow: Yield from Handlers

    [Theory]
    [InlineData("npc.go_to(vec(0,0,0))", "npc.go_to")]
    [InlineData("npc.go_near(vec(0,0,0), 2)", "npc.go_near")]
    [InlineData("npc.follow(1, 2, 3)", "npc.follow")]
    [InlineData("npc.mine(vec(0,0,0))", "npc.mine")]
    [InlineData("npc.place(vec(0,0,0), 'stone')", "npc.place")]
    [InlineData("npc.pick_up(1)", "npc.pick_up")]
    [InlineData("npc.drop('stone', 1)", "npc.drop")]
    [InlineData("npc.give(1, 'stone', 1)", "npc.give")]
    [InlineData("npc.deposit(vec(0,0,0), 'stone', 1)", "npc.deposit")]
    [InlineData("npc.withdraw(vec(0,0,0), 'stone', 1)", "npc.withdraw")]
    [InlineData("npc.wait(1)", "npc.wait")]
    public void YieldFromHandlers_CallingBlockingActions_ThrowsAndFaultsSafely(string actionCall, string expectedName)
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = $@"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker, text)
                {actionCall}
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        host.OnHeard(new EntityId(2), "action");
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains($"blocking function '{expectedName}' cannot be called from an event handler", host.LastError);
    }

    [Fact]
    public void YieldFromHandlers_AcrossAllHandlerTypes_ThrowsAndFaultsSafely()
    {
        // 1. on_attacked
        var (_, host1, _, _) = CreateTestHost();
        host1.LoadScript(@"
            function main() while true do npc.wait(1) end end
            function on_attacked(attacker, damage) npc.wait(1) end
        ");
        host1.Tick(0.1f);
        host1.OnAttacked(new EntityId(2), 5f);
        host1.Tick(0.1f);
        Assert.Equal(BrainStatus.Faulted, host1.Status);
        Assert.Contains("blocking function 'npc.wait' cannot be called from an event handler", host1.LastError);

        // 2. on_player_nearby
        var (_, host2, _, _) = CreateTestHost();
        host2.LoadScript(@"
            function main() while true do npc.wait(1) end end
            function on_player_nearby(player_id) npc.go_to(vec(1, 2, 3)) end
        ");
        host2.Tick(0.1f);
        host2.OnPlayerNearby(new EntityId(2));
        host2.Tick(0.1f);
        Assert.Equal(BrainStatus.Faulted, host2.Status);
        Assert.Contains("blocking function 'npc.go_to' cannot be called from an event handler", host2.LastError);

        // 3. on_interrupted
        var (_, host3, _, _) = CreateTestHost();
        host3.LoadScript(@"
            function main() while true do npc.wait(1) end end
            function on_interrupted(reason) npc.mine(vec(0, 0, 0)) end
        ");
        host3.Tick(0.1f);
        host3.OnInterrupted("reflex");
        host3.Tick(0.1f);
        Assert.Equal(BrainStatus.Faulted, host3.Status);
        Assert.Contains("blocking function 'npc.mine' cannot be called from an event handler", host3.LastError);
    }

    [Theory]
    [InlineData("coroutine.yield()", "unknown global 'coroutine'")]
    [InlineData("__yield()", "unknown global '__yield'")]
    public void YieldFromHandlers_AttemptingDirectCoroutineYield_ThrowsUnknownGlobalAndFaults(string yieldCall, string expectedError)
    {
        var (npc, host, _, _) = CreateTestHost();
        string script = $@"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker, text)
                {yieldCall}
            end
        ";

        host.LoadScript(script);
        host.Tick(0.1f);

        host.OnHeard(new EntityId(2), "yield");
        host.Tick(0.1f);

        Assert.Equal(BrainStatus.Faulted, host.Status);
        Assert.Contains(expectedError, host.LastError);
    }

    #endregion
}
