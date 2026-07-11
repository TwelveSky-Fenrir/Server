using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountAttributeRollerTests
{
    // --- Convert (Sort 6) ---------------------------------------------------------------------------------

    [Fact]
    public void Convert_PicksTheDrawnDigit_WhenNotMaxed()
    {
        // pick=3 -> place 3 (value 1000); the ones-through-thousands digits of 0 are all non-maxed.
        var roll = MountAttributeRoller.Convert(0, new ScriptedRandomSource(3));

        Assert.True(roll.Applied);
        Assert.Equal(1000, roll.PlaceValueAdded);
        Assert.Equal(1000, roll.NewPower);
    }

    [Fact]
    public void Convert_WalksForwardPastAMaxedDigit()
    {
        // Power 9000 has place 3 maxed at 9; pick=3 must walk forward to place 4 (value 10000).
        var roll = MountAttributeRoller.Convert(9000, new ScriptedRandomSource(3));

        Assert.True(roll.Applied);
        Assert.Equal(10000, roll.PlaceValueAdded);
        Assert.Equal(19000, roll.NewPower);
    }

    [Fact]
    public void Convert_WrapsAroundWhenTailDigitsAreMaxed()
    {
        // places 6 and 7 maxed (99_000000); pick=6 walks 6 -> 7 (maxed) -> wraps to place 0 (value 1).
        var roll = MountAttributeRoller.Convert(99_000000, new ScriptedRandomSource(6));

        Assert.True(roll.Applied);
        Assert.Equal(1, roll.PlaceValueAdded);
        Assert.Equal(99_000001, roll.NewPower);
    }

    // --- Delete (Sort 7) ----------------------------------------------------------------------------------

    [Fact]
    public void Delete_DecrementsTheAddressedDigit()
    {
        // attribute-index 8 == ones place; digit 8 -> 7.
        Assert.Equal(12345677, MountAttributeRoller.Delete(12345678, 8));
        // attribute-index 1 == highest place; digit 1 -> 0.
        Assert.Equal(2345678, MountAttributeRoller.Delete(12345678, 1));
    }

    [Fact]
    public void Delete_OnAlreadyZeroDigit_LeavesItZero()
    {
        // ones place already 0 -- no "nothing to delete" guard, digit floors at 0.
        Assert.Equal(12345670, MountAttributeRoller.Delete(12345670, 8));
    }

    // --- Transfer (Sort 8) --------------------------------------------------------------------------------

    [Fact]
    public void Transfer_MovesOnePointToARandomOtherDigit()
    {
        // Source attribute-index 8 (ones, digit 8) -> 7; candidates are places 1..7, pick index 0 -> place 1
        // (digit 7 -> 8). 12345678 -> decrement ones to 7 (12345677) -> place1 7->8 (12345687).
        var roll = MountAttributeRoller.Transfer(12345678, 8, new ScriptedRandomSource(0));

        Assert.True(roll.Applied);
        Assert.Equal(12345687, roll.NewPower);
    }

    [Fact]
    public void Transfer_EmptySourceDigit_Fails()
    {
        // ones place already 0 -- empty source returns 0/failure, power unchanged, no draw consumed.
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
