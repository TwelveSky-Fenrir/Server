using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountAttributeRollerTests
{

    [Fact]
    public void Convert_PicksTheDrawnDigit_WhenNotMaxed()
    {
        var roll = MountAttributeRoller.Convert(0, new ScriptedRandomSource(3));

        Assert.True(roll.Applied);
        Assert.Equal(1000, roll.PlaceValueAdded);
        Assert.Equal(1000, roll.NewPower);
    }

    [Fact]
    public void Convert_WalksForwardPastAMaxedDigit()
    {
        var roll = MountAttributeRoller.Convert(9000, new ScriptedRandomSource(3));

        Assert.True(roll.Applied);
        Assert.Equal(10000, roll.PlaceValueAdded);
        Assert.Equal(19000, roll.NewPower);
    }

    [Fact]
    public void Convert_WrapsAroundWhenTailDigitsAreMaxed()
    {
        var roll = MountAttributeRoller.Convert(99_000000, new ScriptedRandomSource(6));

        Assert.True(roll.Applied);
        Assert.Equal(1, roll.PlaceValueAdded);
        Assert.Equal(99_000001, roll.NewPower);
    }


    [Fact]
    public void Delete_DecrementsTheAddressedDigit()
    {
        Assert.Equal(12345677, MountAttributeRoller.Delete(12345678, 8));
        Assert.Equal(2345678, MountAttributeRoller.Delete(12345678, 1));
    }

    [Fact]
    public void Delete_OnAlreadyZeroDigit_LeavesItZero()
    {
        Assert.Equal(12345670, MountAttributeRoller.Delete(12345670, 8));
    }


    [Fact]
    public void Transfer_MovesOnePointToARandomOtherDigit()
    {
        var roll = MountAttributeRoller.Transfer(12345678, 8, new ScriptedRandomSource(0));

        Assert.True(roll.Applied);
        Assert.Equal(12345687, roll.NewPower);
    }

    [Fact]
    public void Transfer_EmptySourceDigit_Fails()
    {
        var roll = MountAttributeRoller.Transfer(12345670, 8, new ScriptedRandomSource(0));

        Assert.False(roll.Applied);
        Assert.Equal(12345670, roll.NewPower);
    }

    [Fact]
    public void Transfer_TotalInvestedPointsIsPreserved()
    {
        var before = MountPowerCodec.DigitSum(12345678);
        var roll = MountAttributeRoller.Transfer(12345678, 8, new ScriptedRandomSource(2));

        Assert.True(roll.Applied);
        Assert.Equal(before, MountPowerCodec.DigitSum(roll.NewPower));
    }

    private sealed class ScriptedRandomSource(params int[] sequence) : IRandomSource
    {
        private int _index;

        public int NextInt32(int exclusiveUpperBound)
        {
            return sequence[_index++] % exclusiveUpperBound;
        }
    }
}
