namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Linq;
using VoxelKing.Core.Brain;
using VoxelKing.Core.Common;
using Xunit;

public class BrainValidatorTests
{
    [Fact]
    public void Validate_EmptyScript_ReturnsStage2Error()
    {
        var validator = new BrainValidator();
        var result = validator.Validate("");

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(2, err.Stage);
    }

    [Fact]
    public void Validate_WhitespaceScript_ReturnsStage2Error()
    {
        var validator = new BrainValidator();
        var result = validator.Validate("   \n\t   ");

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(2, err.Stage);
    }

    [Fact]
    public void Validate_OversizedScript_ReturnsStage2Error()
    {
        var validator = new BrainValidator();
        string hugeScript = "function main()\n" + new string(' ', 9000) + "\nend";
        var result = validator.Validate(hugeScript);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(2, err.Stage);
        Assert.Contains("exceeds limit", err.Message);
    }

    [Fact]
    public void Validate_SyntaxError_ReturnsStage3ErrorWithLine()
    {
        var validator = new BrainValidator();
        string broken = "function main(\n npc.say('bad')\n";
        var result = validator.Validate(broken);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(3, err.Stage);
        Assert.NotNull(err.Line);
    }

    [Fact]
    public void Validate_MissingMainFunction_ReturnsStage4Error()
    {
        var validator = new BrainValidator();
        string noMain = "local a = 1\nlocal function helper() end";
        var result = validator.Validate(noMain);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(4, err.Stage);
        Assert.Contains("main()", err.Message);
    }

    [Fact]
    public void Validate_MainNotAFunction_ReturnsStage4Error()
    {
        var validator = new BrainValidator();
        string nonFuncMain = "main = 42";
        var result = validator.Validate(nonFuncMain);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(4, err.Stage);
        Assert.Contains("function", err.Message);
    }

    [Fact]
    public void Validate_TopLevelInstructionBudgetExceeded_ReturnsStage4Error()
    {
        var limits = new BrainLimitsConfig { MaxTopLevelChunkInstructions = 100 };
        var validator = new BrainValidator(limits);
        string infiniteLoop = @"
            local counter = 0
            while true do
                counter = counter + 1
            end
            function main() end
        ";
        var result = validator.Validate(infiniteLoop);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(4, err.Stage);
        Assert.Contains("instruction budget exceeded", err.Message);
    }

    [Fact]
    public void Validate_UnauthorizedGlobalVariable_ReturnsStage4Error()
    {
        var validator = new BrainValidator();
        string script = @"
            my_unauthorized_global = 123
            function main()
                npc.say('hello')
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(4, err.Stage);
        Assert.Contains("my_unauthorized_global", err.Message);
    }

    [Fact]
    public void Validate_UnauthorizedGlobalFunction_ReturnsStage4Error()
    {
        var validator = new BrainValidator();
        string script = @"
            function helper_func()
                return 42
            end
            function main()
                npc.say('hello')
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(4, err.Stage);
        Assert.Contains("helper_func", err.Message);
    }

    [Fact]
    public void Validate_LocalHelperAndHandlers_PassStage4()
    {
        var validator = new BrainValidator();
        string script = @"
            local function helper()
                return 42
            end

            function main()
                local val = helper()
                npc.say('val is ' .. val)
                npc.done('finished')
            end

            function on_heard(speaker_id, text)
                npc.say('heard: ' .. text)
            end
        ";
        var result = validator.Validate(script);
        Assert.True(result.Ok, $"Validation failed: {result}");
    }

    [Fact]
    public void Validate_DryRun_UnknownGlobalInMain_ReturnsStage5Error()
    {
        var validator = new BrainValidator();
        string script = @"
            function main()
                some_nonexistent_global.call_me()
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.Stage);
        Assert.Contains("unknown global", err.Message);
    }

    [Fact]
    public void Validate_DryRun_UnknownApiInMain_ReturnsStage5Error()
    {
        var validator = new BrainValidator();
        string script = @"
            function main()
                world.teleport_player_to_orbit(123)
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.Stage);
        Assert.Contains("nil value", err.Message);
    }

    [Fact]
    public void Validate_DryRun_BusyLoopInMain_ReturnsStage5Error()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerTick = 100,
            MaxConsecutiveForcedYields = 5
        };
        var validator = new BrainValidator(limits);
        string script = @"
            function main()
                local count = 0
                while true do
                    count = count + 1
                end
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.Stage);
        Assert.Contains("busy loop", err.Message);
    }

    [Fact]
    public void Validate_DryRun_WrongActionArgumentType_ReturnsStage5Error()
    {
        var validator = new BrainValidator();
        string script = @"
            function main()
                npc.mine('not_a_pos_table')
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.Stage);
        Assert.Contains("pos", err.Message);
    }

    [Fact]
    public void Validate_DryRun_BlockingActionInEventHandler_ReturnsStage5Error()
    {
        var validator = new BrainValidator();
        string script = @"
            function main()
                while true do npc.wait(1) end
            end

            function on_heard(speaker, text)
                npc.go_to(vec(0, 64, 0))
            end
        ";
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.Stage);
        Assert.Contains("cannot be called from an event handler", err.Message);
    }

    [Fact]
    public void Validate_DryRun_HandlerInstructionBudgetExceeded_ReturnsStage5Error()
    {
        var limits = new BrainLimitsConfig
        {
            MaxInstructionsPerHandler = 200
        };
        var validator = new BrainValidator(limits);
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
        var result = validator.Validate(script);

        Assert.False(result.Ok);
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.Stage);
        Assert.Contains("instruction budget", err.Message);
    }

    [Fact]
    public void Validate_AllValidExampleBrains_PassSuccessfully()
    {
        var validator = new BrainValidator();
        var examples = BrainExampleLibrary.LoadAll().Where(e => e.IsValid).ToList();
        Assert.NotEmpty(examples);

        foreach (var example in examples)
        {
            var result = validator.Validate(example.Script);
            Assert.True(result.Ok, $"Example '{example.Name}' failed validation:\n{result}");
        }
    }

    [Fact]
    public void Validate_EachInvalidExampleBrain_FailsAtExpectedStage()
    {
        var validator = new BrainValidator();
        var examples = BrainExampleLibrary.LoadAll().Where(e => !e.IsValid).ToList();
        Assert.NotEmpty(examples);

        var byName = examples.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        // 1. syntax_error fails at stage 3
        Assert.True(byName.ContainsKey("syntax_error"));
        var synRes = validator.Validate(byName["syntax_error"].Script);
        Assert.False(synRes.Ok);
        Assert.Contains(synRes.Errors, e => e.Stage == 3);

        // 2. unknown_api fails at stage 5
        Assert.True(byName.ContainsKey("unknown_api"));
        var unkRes = validator.Validate(byName["unknown_api"].Script);
        Assert.False(unkRes.Ok);
        Assert.Contains(unkRes.Errors, e => e.Stage == 5);

        // 3. busy_loop fails at stage 5
        Assert.True(byName.ContainsKey("busy_loop"));
        var busyRes = validator.Validate(byName["busy_loop"].Script);
        Assert.False(busyRes.Ok);
        Assert.Contains(busyRes.Errors, e => e.Stage == 5);

        // 4. blocking_in_handler fails at stage 5
        Assert.True(byName.ContainsKey("blocking_in_handler"));
        var blkRes = validator.Validate(byName["blocking_in_handler"].Script);
        Assert.False(blkRes.Ok);
        Assert.Contains(blkRes.Errors, e => e.Stage == 5);
    }
}
