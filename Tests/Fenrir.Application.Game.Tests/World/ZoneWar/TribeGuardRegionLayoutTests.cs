using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardRegionLayoutTests
{
    [Fact]
    public void RegionConstants_MatchTheLegacyValues()
    {
        Assert.Equal(3400, TribeGuardRegionLayout.NormalGuardRegionLegacyStart);
        Assert.Equal(3500, TribeGuardRegionLayout.TribeGuardRegionLegacyStart);
        Assert.Equal(100, TribeGuardRegionLayout.RegionSlotCount);
        Assert.Equal(5, TribeGuardRegionLayout.SlotsPerTribeSlot);
        Assert.Equal(5, TribeGuardRegionLayout.TribeSlotCount);
        Assert.Equal(25, TribeGuardRegionLayout.MaxUsedSlots);
        Assert.True(TribeGuardRegionLayout.MaxUsedSlots <= TribeGuardRegionLayout.RegionSlotCount);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 4, 4)]
    [InlineData(1, 0, 5)]
    [InlineData(1, 4, 9)]
    [InlineData(3, 0, 15)]
    [InlineData(4, 4, 24)]
    public void RelativeReservedIndex_IsRegionStartPlusFiveTimesOrdinalPlusPost(int ordinal, int post, int expected)
    {
        Assert.Equal(expected, TribeGuardRegionLayout.RelativeReservedIndex(ordinal, post));
    }

    [Fact]
    public void EveryTribeSlot_OwnsADisjointFiveIndexBlock_NoTwoSlotsCollide()
    {
        var seen = new HashSet<int>();
        for (var ordinal = 0; ordinal < TribeGuardRegionLayout.TribeSlotCount; ordinal++)
        for (var post = 0; post < TribeGuardRegionLayout.SlotsPerTribeSlot; post++)
        {
            var index = TribeGuardRegionLayout.RelativeReservedIndex(ordinal, post);

            Assert.InRange(index, ordinal * 5, ordinal * 5 + 4);
            Assert.True(seen.Add(index), $"index {index} collided across tribe-slots");
        }

        Assert.Equal(TribeGuardRegionLayout.MaxUsedSlots, seen.Count);
    }

    [Fact]
    public void TwoDifferentTribeSlots_NeverShareAReservedIndex()
    {
        var slotZeroPostZero = TribeGuardRegionLayout.RelativeReservedIndex(0, 0);
        var slotThreePostZero = TribeGuardRegionLayout.RelativeReservedIndex(3, 0);
        Assert.NotEqual(slotZeroPostZero, slotThreePostZero);
        Assert.Equal(0, slotZeroPostZero);
        Assert.Equal(15, slotThreePostZero);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 5)]
    public void RelativeReservedIndex_OutOfRangeArgument_Throws(int ordinal, int post)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TribeGuardRegionLayout.RelativeReservedIndex(ordinal, post));
    }

    [Fact]
    public void BuildDeterministicSlots_StampsEachCoordinateWithItsOwnersDeterministicIndex()
    {
        var coords = new (float X, float Y, float Z)[]
        {
            (10f, 1f, 20f),
            (30f, 2f, 40f),
            (50f, 3f, 60f)
        };

        var slots = TribeGuardRegionLayout.BuildDeterministicSlots(2, coords);

        Assert.Equal(3, slots.Length);
        Assert.Equal(10, slots[0].ReservedSlotIndex);
        Assert.Equal(11, slots[1].ReservedSlotIndex);
        Assert.Equal(12, slots[2].ReservedSlotIndex);
        Assert.Equal(10f, slots[0].X);
        Assert.Equal(1f, slots[0].Y);
        Assert.Equal(60f, slots[2].Z);
    }

    [Fact]
    public void BuildDeterministicSlots_MoreThanFiveCoordinates_Throws()
    {
        var tooMany = new (float, float, float)[6];
        Assert.Throws<ArgumentOutOfRangeException>(() => TribeGuardRegionLayout.BuildDeterministicSlots(0, tooMany));
    }

    [Fact]
    public void BuildDeterministicSlots_EmptyPost_IsAnEmptyArray()
    {
        var slots = TribeGuardRegionLayout.BuildDeterministicSlots(0, []);
        Assert.True(slots.IsEmpty);
    }
}
