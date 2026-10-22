namespace VoxelKing.Core.Tests.Brain;

using System;
using System.Linq;
using VoxelKing.Core.Brain;
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
    }

    [Fact]
    public void Validate_MissingMainFunction_ReturnsStage4Error()
    {
        var validator = new BrainValidator();
        string noMain = "local a = 1\nfunction helper() end";
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
}
