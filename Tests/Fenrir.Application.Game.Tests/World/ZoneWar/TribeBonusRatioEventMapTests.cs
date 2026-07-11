using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeBonusRatioEventMapTests
{
    private static byte[] Payload(int eventSort, int eventValue)
    {
        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0), eventSort);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), eventValue);
        return data;
    }

    [Theory]
    [InlineData(21, 0)]
    [InlineData(22, 1)]
    [InlineData(23, 2)]
    [InlineData(24, 3)]
    public void GeneralExperienceUpFamily_MapsCodeToTribeIndex_AndScalesByDecile(int eventSort, byte expectedTribe)
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(eventSort, 1));

        Assert.Equal(0.1f, state.GetExperienceBonusRatio(expectedTribe));
        for (byte tribe = 0; tribe < 4; tribe++)
            if (tribe != expectedTribe)
                Assert.Equal(0f, state.GetExperienceBonusRatio(tribe));
    }

    [Theory]
    [InlineData(31, 0)]
    [InlineData(32, 1)]
    [InlineData(33, 2)]
    [InlineData(34, 3)]
    public void ItemDropUpFamily_MapsCodeToTribeIndex_AndScalesByDecile(int eventSort, byte expectedTribe)
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(eventSort, 2));

        Assert.Equal(0.2f, state.GetItemDropBonusRatio(expectedTribe));
        for (byte tribe = 0; tribe < 4; tribe++)
            if (tribe != expectedTribe)
                Assert.Equal(0f, state.GetItemDropBonusRatio(tribe));
    }

    [Theory]
    [InlineData(41, 0)]
    [InlineData(42, 1)]
    [InlineData(43, 2)]
    [InlineData(44, 3)]
    public void ItemDropUpForMyoungFamily_MapsCodeToTribeIndex_AndScalesByDecile(int eventSort, byte expectedTribe)
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(eventSort, 5));

        Assert.Equal(0.5f, state.GetMyoungItemDropBonusRatio(expectedTribe));
        for (byte tribe = 0; tribe < 4; tribe++)
            if (tribe != expectedTribe)
                Assert.Equal(0f, state.GetMyoungItemDropBonusRatio(tribe));
    }

    [Theory]
    [InlineData(51, 0)]
    [InlineData(52, 1)]
    [InlineData(53, 2)]
    [InlineData(54, 3)]
    public void KillOtherTribeAddValueFamily_MapsCodeToTribeIndex_StoresVerbatim_NoDecileScale(int eventSort,
        byte expectedTribe)
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(eventSort, 7));

        Assert.Equal(7, state.GetKillOtherTribeBonus(expectedTribe));
    }

    [Fact]
    public void KillOtherTribeAddValueFamily_SettingOneTribe_ZeroesTheOtherThree()
    {
        var state = new ZoneCenterSiegeState();
        state.SetKillOtherTribeBonus(1, 9);

        TribeBonusRatioEventMap.Apply(state, Payload(51, 4));

        Assert.Equal(4, state.GetKillOtherTribeBonus(0));
        Assert.Equal(0, state.GetKillOtherTribeBonus(1));
        Assert.Equal(0, state.GetKillOtherTribeBonus(2));
        Assert.Equal(0, state.GetKillOtherTribeBonus(3));
    }

    [Fact]
    public void OneLiveProductionTriggerShape_EventValueOfOne_YieldsExactlyTenPercent()
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(21, 1));
        TribeBonusRatioEventMap.Apply(state, Payload(31, 1));
        TribeBonusRatioEventMap.Apply(state, Payload(41, 1));
        TribeBonusRatioEventMap.Apply(state, Payload(51, 1));

        Assert.Equal(0.1f, state.GetExperienceBonusRatio(0));
        Assert.Equal(0.1f, state.GetItemDropBonusRatio(0));
        Assert.Equal(0.1f, state.GetMyoungItemDropBonusRatio(0));
        Assert.Equal(1, state.GetKillOtherTribeBonus(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(35)]
    [InlineData(40)]
    [InlineData(45)]
    [InlineData(50)]
    [InlineData(55)]
    [InlineData(302)]
    public void UnrecognizedEventSort_WritesNothingToAnyOfTheFourArrays(int eventSort)
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(eventSort, 999));

        for (byte tribe = 0; tribe < 4; tribe++)
        {
            Assert.Equal(0f, state.GetExperienceBonusRatio(tribe));
            Assert.Equal(0f, state.GetItemDropBonusRatio(tribe));
            Assert.Equal(0f, state.GetMyoungItemDropBonusRatio(tribe));
            Assert.Equal(0, state.GetKillOtherTribeBonus(tribe));
        }
    }

    [Fact]
    public void NegativeEventValue_ScalesAndStoresCorrectly_ForBothRatioAndVerbatimFamilies()
    {
        var state = new ZoneCenterSiegeState();

        TribeBonusRatioEventMap.Apply(state, Payload(23, -5));
        TribeBonusRatioEventMap.Apply(state, Payload(53, -2));

        Assert.Equal(-0.5f, state.GetExperienceBonusRatio(2));
        Assert.Equal(-2, state.GetKillOtherTribeBonus(2));
    }

    [Fact]
    public void EventCode_IsExactlyLegacyTSort301()
    {
        Assert.Equal(301, TribeBonusRatioEventMap.EventCode);
    }
}
