namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using Xunit;

public class SandboxTests
{
    [Fact]
    public void CreateScript_ReturnsValidSandboxedScript()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        Assert.NotNull(script);
        Assert.Null(script.Options.ScriptLoader);

        var result = script.DoString("return 1 + 1");
        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void CreateScript_SupportsCustomRandomProvider()
    {
        var provider = new DefaultRandomProvider(999);
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript(provider);

        Assert.NotNull(script);
        var res = script.DoString("return random(1, 100)");
        Assert.InRange(res.Number, 1, 100);
    }

    [Fact]
    public void CreateScript_PreservesBrainLimitsConfig()
    {
        var limits = new BrainLimitsConfig { MaxInstructionsPerTick = 1234 };
        var runtime = new MoonSharpRuntime(limits);

        Assert.Same(limits, runtime.LimitsConfig);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("os")]
    [InlineData("load")]
    [InlineData("loadstring")]
    [InlineData("dofile")]
    [InlineData("require")]
    [InlineData("debug")]
    [InlineData("coroutine")]
    [InlineData("print")]
    [InlineData("rawset")]
    [InlineData("rawget")]
    [InlineData("package")]
    [InlineData("dynamic")]
    [InlineData("json")]
    [InlineData("collectgarbage")]
    [InlineData("module")]
    [InlineData("loadfile")]
    [InlineData("setfenv")]
    [InlineData("getfenv")]
    public void ForbiddenGlobals_AreAbsentAndTriggerUnknownGlobalError(string globalName)
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        // 1. C# access returns nil
        Assert.True(script.Globals.Get(globalName).IsNil());

        // 2. Direct identifier access in Lua throws unknown global error
        var exDirect = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString($"local val = {globalName}");
        });
        Assert.Contains($"unknown global '{globalName}'", exDirect.Message);

        // 3. _G lookup in Lua throws unknown global error
        var exLookup = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString($"local val = _G['{globalName}']");
        });
        Assert.Contains($"unknown global '{globalName}'", exLookup.Message);
    }

    [Fact]
    public void StringDump_IsRemovedFromLua()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        // C# check: dump is not in string table
        var stringTable = script.Globals.Get("string").Table;
        Assert.NotNull(stringTable);
        Assert.True(stringTable.Get("dump").IsNil());

        // Calling string.dump directly throws attempt to call a nil value
        var exDirect = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("string.dump(function() end)");
        });
        Assert.Contains("attempt to call a nil value", exDirect.Message);

        // Calling string method ("str"):dump() throws attempt to call a nil value
        var exMethod = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("('abc'):dump()");
        });
        Assert.Contains("attempt to call a nil value", exMethod.Message);
    }

    [Fact]
    public void MathRandomSeed_IsRemovedFromLua()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var mathTable = script.Globals.Get("math").Table;
        Assert.NotNull(mathTable);
        Assert.True(mathTable.Get("randomseed").IsNil());

        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("math.randomseed(12345)");
        });
        Assert.Contains("attempt to call a nil value", ex.Message);
    }

    [Fact]
    public void UnknownGlobalTrap_CatchesHallucinatedApis()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var exVar = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("local x = npc_jump");
        });
        Assert.Contains("unknown global 'npc_jump'", exVar.Message);

        var exFunc = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("teleport(0, 0, 0)");
        });
        Assert.Contains("unknown global 'teleport'", exFunc.Message);

        var exCond = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("if hallucinated_property then end");
        });
        Assert.Contains("unknown global 'hallucinated_property'", exCond.Message);

        var exTable = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("local x = non_existent_table.foo");
        });
        Assert.Contains("unknown global 'non_existent_table'", exTable.Message);
    }

    [Fact]
    public void DefinedGlobals_CanBeReadWithoutTriggeringTrap()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        script.DoString("CONFIG_VALUE = 42");
        var val = script.DoString("return CONFIG_VALUE").Number;
        Assert.Equal(42, val);

        script.DoString("function custom_sum(a, b) return a + b end");
        var sum = script.DoString("return custom_sum(15, 27)").Number;
        Assert.Equal(42, sum);

        // Setting a global to nil causes subsequent reads to trigger unknown global trap
        script.DoString("CONFIG_VALUE = nil");
        var ex = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("return CONFIG_VALUE");
        });
        Assert.Contains("unknown global 'CONFIG_VALUE'", ex.Message);
    }

    [Fact]
    public void DeterministicRandom_ProducesIdenticalSequencesWithSameSeed()
    {
        var runtime = new MoonSharpRuntime();
        var script1 = runtime.CreateScript(12345);
        var script2 = runtime.CreateScript(12345);

        for (int i = 0; i < 25; i++)
        {
            var v1 = script1.DoString("return random(-20, 20)").Number;
            var v2 = script2.DoString("return random(-20, 20)").Number;
            Assert.Equal(v1, v2);
            Assert.InRange(v1, -20, 20);
        }

        // Test random() without arguments
        for (int i = 0; i < 10; i++)
        {
            var f1 = script1.DoString("return random()").Number;
            var f2 = script2.DoString("return random()").Number;
            Assert.Equal(f1, f2);
            Assert.InRange(f1, 0.0, 1.0);
        }

        // Test random(max) with 1 argument
        for (int i = 0; i < 10; i++)
        {
            var m1 = script1.DoString("return random(50)").Number;
            var m2 = script2.DoString("return random(50)").Number;
            Assert.Equal(m1, m2);
            Assert.InRange(m1, 1, 50);
        }
    }

    [Fact]
    public void DeterministicRandom_ProducesDifferentSequencesWithDifferentSeeds()
    {
        var runtime = new MoonSharpRuntime();
        var script1 = runtime.CreateScript(111);
        var script2 = runtime.CreateScript(222);

        var list1 = new List<double>();
        var list2 = new List<double>();

        for (int i = 0; i < 20; i++)
        {
            list1.Add(script1.DoString("return random(1, 1000)").Number);
            list2.Add(script2.DoString("return random(1, 1000)").Number);
        }

        Assert.NotEqual(list1, list2);
    }

    [Fact]
    public void DeterministicRandom_MathRandomRoutesToSameGenerator()
    {
        var runtime = new MoonSharpRuntime();
        var script1 = runtime.CreateScript(42);
        var script2 = runtime.CreateScript(42);

        for (int i = 0; i < 15; i++)
        {
            var v1 = script1.DoString("return random(1, 100)").Number;
            var v2 = script2.DoString("return math.random(1, 100)").Number;
            Assert.Equal(v1, v2);
        }
    }

    [Fact]
    public void DeterministicRandom_HandlesEdgeCasesAndValidation()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript(42);

        // a == b returns a
        var same = script.DoString("return random(7, 7)").Number;
        Assert.Equal(7, same);

        // Empty interval (a > b)
        var exInverted = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("return random(10, 5)");
        });
        Assert.Contains("bad argument #2 to 'random' (interval is empty)", exInverted.Message);

        // Single arg max < 1
        var exMax = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("return random(0)");
        });
        Assert.Contains("bad argument #1 to 'random' (interval is empty)", exMax.Message);

        // Type mismatch on argument 1
        var exType1 = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("return random('abc')");
        });
        Assert.Contains("bad argument #1 to 'random' (number expected, got string)", exType1.Message);

        // Type mismatch on argument 2
        var exType2 = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("return random(1, 'abc')");
        });
        Assert.Contains("bad argument #2 to 'random' (number expected, got string)", exType2.Message);
    }

    [Fact]
    public void MetatableProtection_PreventsModifyingOrInspectingGlobalsMetatable()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var gmRes = script.DoString("return getmetatable(_G)").String;
        Assert.Equal("protected", gmRes);

        var exSet = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("setmetatable(_G, {})");
        });
        Assert.Contains("cannot change a protected metatable", exSet.Message);
    }

    [Fact]
    public void ProtectTable_ProtectsCustomTables()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        var customTable = new Table(script);
        LuaSandbox.ProtectTable(customTable, script);
        script.Globals["api_test"] = customTable;

        var gmRes = script.DoString("return getmetatable(api_test)").String;
        Assert.Equal("protected", gmRes);

        var exSet = Assert.Throws<ScriptRuntimeException>(() =>
        {
            script.DoString("setmetatable(api_test, {})");
        });
        Assert.Contains("cannot change a protected metatable", exSet.Message);
    }

    [Fact]
    public void StandardLibraries_MathStringTableWorkCorrectly()
    {
        var runtime = new MoonSharpRuntime();
        var script = runtime.CreateScript();

        // math
        var mathRes = script.DoString("return math.floor(math.abs(-4.7))").Number;
        Assert.Equal(4, mathRes);

        var mathMax = script.DoString("return math.max(10, 20)").Number;
        Assert.Equal(20, mathMax);

        // string
        var strRes = script.DoString("return string.upper('voxel') .. string.sub('king_world', 1, 5)").String;
        Assert.Equal("VOXELking_", strRes);

        // table
        script.DoString(@"
            local t = { 3, 1, 2 }
            table.sort(t)
            SORTED = table.concat(t, ',')
        ");
        var sorted = script.DoString("return SORTED").String;
        Assert.Equal("1,2,3", sorted);
    }
}
