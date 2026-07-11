using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="PopupEventZoneCatalog" /> (the <c>IsPopUp*</c> per-map gating + <c>tKillReq</c>
///     thresholds) and the <see cref="PopupEventType" /> enum ordering, both anchored to
///     <c>Server/Header/function.h:3258-3344</c> and <c>Server/Header/Protocol/STRUCT.h:647-661</c>.
/// </summary>
public class PopupEventZoneCatalogTests
{
    [Fact]
    public void EnumOrdering_MatchesLegacyPopUpType()
    {
        // STRUCT.h:647-661 -- indexing into mPopUpTypeState[5] / WorldInfo.PopUpTypeState[5] depends on this.
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
    [InlineData((short)145)] // Monster/PvE map -- a distinct event kind, never a PvP type
    [InlineData((short)104)]
    [InlineData((short)999)] // outside every set
    [InlineData((short)0)]
    [InlineData((short)164)] // LNW33 test shard is NOT a war-popup map
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
    [InlineData((short)146)] // RegularWar, not Monster
    [InlineData((short)38)] // Yanggok, not Monster
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
        // S07_MyGame03.cpp:2737-2748 (LNW33, live in ReleaseEU33). Applies only to the war-counter types.
        Assert.Equal(1, PopupEventZoneCatalog.KillThreshold(PopupEventType.RegularWar, 164));
        Assert.Equal(1, PopupEventZoneCatalog.KillThreshold(PopupEventType.RuinsPvp, 164));

        // Non-war types are unaffected by the 164 shard.
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
        foreach (PopupEventType type in Enum.GetValues<PopupEventType>())
            Assert.False(state.IsEnabled(type));

        state.SetEnabled(PopupEventType.YanggokPvp, true);
        Assert.True(state.IsEnabled(PopupEventType.YanggokPvp));
        Assert.False(state.IsEnabled(PopupEventType.RegularWar)); // independent flags

        state.SetEnabled(PopupEventType.YanggokPvp, false);
        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
    }
}
