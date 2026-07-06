using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Crafting;

public class RuneStoneStatRollTableTests
{
    [Theory]
    [InlineData(195, 26)]
    [InlineData(196, 27)]
    [InlineData(197, 28)]
    [InlineData(198, 29)]
    [InlineData(199, 30)]
    public void TopTier_Draws195To199_Yield26To30(int draw, int expected)
    {
        Assert.Equal(expected, RuneStoneStatRollTable.RollFromDraw(draw));
    }

    [Theory]
    [InlineData(175, 20)]
    [InlineData(178, 20)]
    [InlineData(179, 20)]
    [InlineData(180, 21)]
    [InlineData(194, 24)]
    public void UpperMidTier_Draws175To194_Yield20To24(int draw, int expected)
    {
        Assert.Equal(expected, RuneStoneStatRollTable.RollFromDraw(draw));
    }

    [Theory]
    [InlineData(125, 15)]
    [InlineData(134, 15)]
    [InlineData(135, 16)]
    [InlineData(174, 19)]
    public void LowerMidTier_Draws125To174_Yield15To19(int draw, int expected)
    {
        Assert.Equal(expected, RuneStoneStatRollTable.RollFromDraw(draw));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(124, 14)]
    public void BaseTier_Draws0To124_Yield1To14(int draw, int expected)
    {
        Assert.Equal(expected, RuneStoneStatRollTable.RollFromDraw(draw));
    }

    [Fact]
    public void BaseTier_NeverExceeds14()
    {
        for (var draw = 0; draw < 125; draw++)
            Assert.InRange(RuneStoneStatRollTable.RollFromDraw(draw), 1, 14);
    }

    [Fact]
    public void EveryDrawInRange_ProducesValueClampedTo30()
    {
        for (var draw = 0; draw < RuneStoneStatRollTable.DrawRange; draw++)
            Assert.InRange(RuneStoneStatRollTable.RollFromDraw(draw), 1, RuneStoneStatRollTable.HardCeiling);
    }

    [Fact]
    public void Roll_DrawsFromA200ValueRange()
    {
        var random = new ScriptedRandomSource(199);
        var value = RuneStoneStatRollTable.Roll(random);

        Assert.Equal(30, value);
    }
}
