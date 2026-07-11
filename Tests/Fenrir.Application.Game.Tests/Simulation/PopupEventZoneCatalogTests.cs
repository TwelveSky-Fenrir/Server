using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public class PopupEventZoneCatalogTests
{
    [Fact]
    public void EnumOrdering_MatchesLegacyPopUpType()
    {
        Assert.Equal(0, (int)PopupEventType.RegularWar);
        Assert.Equal(1, (int)PopupEventType.YanggokPvp);
        Assert.Equal(2, (int)PopupEventType.MonsterPve);
        Assert.Equal(3, (int)PopupEventType.InvasionPvp);
        Assert.Equal(4, (int)PopupEventType.RuinsPvp);
    }

    [Theory]
    [InlineData((short)146, PopupEventType.RegularWar)]
    [InlineData((short)160, PopupEventType.RegularWar)]
    [InlineData((short)38, PopupEventType.YanggokPvp)]
    [InlineData((short)1, PopupEventType.InvasionPvp)]
    [InlineData((short)6, PopupEventType.InvasionPvp)]
    [InlineData((short)11, PopupEventType.InvasionPvp)]
    [InlineData((short)140, PopupEventType.InvasionPvp)]
    [InlineData((short)268, PopupEventType.RuinsPvp)]
    public void TryResolvePvpType_KnownMap_ResolvesExpectedType(short mapId, PopupEventType expected)
    {
        Assert.True(PopupEventZoneCatalog.TryResolvePvpType(mapId, out var type));
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData((short)145)]
    [InlineData((short)104)]
    [InlineData((short)999)]
    [InlineData((short)0)]
    [InlineData((short)164)]
    public void TryResolvePvpType_NonPvpOrUnknownMap_ReturnsFalse(short mapId)
    {
        Assert.False(PopupEventZoneCatalog.TryResolvePvpType(mapId, out _));
    }

    [Theory]
    [InlineData((short)104)]
    [InlineData((short)109)]
    [InlineData((short)145)]
    [InlineData((short)174)]
    public void IsMonsterPopupMap_KnownMonsterMap_True(short mapId)
    {
        Assert.True(PopupEventZoneCatalog.IsMonsterPopupMap(mapId));
    }

    [Theory]
    [InlineData((short)146)]
    [InlineData((short)38)]
    [InlineData((short)999)]
    public void IsMonsterPopupMap_NonMonsterMap_False(short mapId)
    {
        Assert.False(PopupEventZoneCatalog.IsMonsterPopupMap(mapId));
    }

    [Fact]
    public void KillThreshold_PerType_MatchesLegacyKillReq()
    {
        Assert.Equal(10, PopupEventZoneCatalog.KillThreshold(PopupEventType.RegularWar, 146));
        Assert.Equal(10, PopupEventZoneCatalog.KillThreshold(PopupEventType.RuinsPvp, 268));
        Assert.Equal(10, PopupEventZoneCatalog.KillThreshold(PopupEventType.YanggokPvp, 38));
        Assert.Equal(5, PopupEventZoneCatalog.KillThreshold(PopupEventType.InvasionPvp, 1));
        Assert.Equal(400, PopupEventZoneCatalog.KillThreshold(PopupEventType.MonsterPve, 145));
    }

    [Fact]
    public void KillThreshold_WarType_OnShard164_IsForcedToOne_LnwOverride()
    {
        Assert.Equal(1, PopupEventZoneCatalog.KillThreshold(PopupEventType.RegularWar, 164));
        Assert.Equal(1, PopupEventZoneCatalog.KillThreshold(PopupEventType.RuinsPvp, 164));

        Assert.Equal(10, PopupEventZoneCatalog.KillThreshold(PopupEventType.YanggokPvp, 164));
        Assert.Equal(5, PopupEventZoneCatalog.KillThreshold(PopupEventType.InvasionPvp, 164));
    }

    [Theory]
    [InlineData(PopupEventType.RegularWar, true)]
    [InlineData(PopupEventType.RuinsPvp, true)]
    [InlineData(PopupEventType.YanggokPvp, false)]
    [InlineData(PopupEventType.InvasionPvp, false)]
    [InlineData(PopupEventType.MonsterPve, false)]
    public void UsesWarCounter_OnlyRegularWarAndRuins(PopupEventType type, bool expected)
    {
        Assert.Equal(expected, PopupEventZoneCatalog.UsesWarCounter(type));
    }

    [Fact]
    public void PopupEventState_DefaultsAllDisabled_AndTogglesPerType()
    {
        var state = new PopupEventState();
        foreach (var type in Enum.GetValues<PopupEventType>())
            Assert.False(state.IsEnabled(type));

        state.SetEnabled(PopupEventType.YanggokPvp, true);
        Assert.True(state.IsEnabled(PopupEventType.YanggokPvp));
        Assert.False(state.IsEnabled(PopupEventType.RegularWar));

        state.SetEnabled(PopupEventType.YanggokPvp, false);
        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
    }
}
