using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillExtendedRewardZonesTests
{
    private static readonly PvpKillZoneRewardProfile FullReward = new(true, true, true, true, 0);

    [Theory]
    [InlineData((short)2)]
    [InlineData((short)7)]
    [InlineData((short)14)]
    [InlineData((short)141)]
    [InlineData((short)143)]
    public void SymbolBattleZone_WhenBattleActive_GrantsFullSet(short zoneId)
    {
        var profile = PvpKillExtendedRewardZones.TryResolve(zoneId, false,
            new PvpKillRewardZoneRuntimeState(SymbolBattleActive: true));

        Assert.NotNull(profile);
        Assert.Equal(FullReward, profile.Value);
    }

    [Theory]
    [InlineData((short)2)]
    [InlineData((short)143)]
    public void SymbolBattleZone_WhenBattleInactive_GrantsNothing(short zoneId)
    {
        var profile = PvpKillExtendedRewardZones.TryResolve(zoneId, false, PvpKillRewardZoneRuntimeState.Inactive);

        Assert.NotNull(profile);
        Assert.Equal(PvpKillZoneRewardProfile.None, profile.Value);
    }

    [Fact]
    public void SymbolBattleZone_IsUnaffectedByStunTrigger()
    {
        var active = new PvpKillRewardZoneRuntimeState(SymbolBattleActive: true);

        Assert.Equal(
            PvpKillExtendedRewardZones.TryResolve(2, false, active),
            PvpKillExtendedRewardZones.TryResolve(2, true, active));
    }

    [Fact]
    public void DtmZone38_AlwaysForceEnablesXpAndCp_ButGatesDropAndDailyOnRuntime()
    {
        var withoutRuntime = PvpKillExtendedRewardZones.TryResolve(
            PvpKillExtendedRewardZones.DtmZoneId, false, PvpKillRewardZoneRuntimeState.Inactive);

        Assert.NotNull(withoutRuntime);
        Assert.Equal(new PvpKillZoneRewardProfile(true, true, false, false, 0), withoutRuntime.Value);
    }

    [Fact]
    public void DtmZone38_WithRuntimeDropAndDaily_GrantsFullSet()
    {
        var withRuntime = PvpKillExtendedRewardZones.TryResolve(
            PvpKillExtendedRewardZones.DtmZoneId, false,
            new PvpKillRewardZoneRuntimeState(Map38DropEnabled: true, Map38DailyMissionEnabled: true));

        Assert.NotNull(withRuntime);
        Assert.Equal(FullReward, withRuntime.Value);
    }

    [Fact]
    public void DtmZone38_XpAndCpUnaffectedByStunTrigger()
    {
        var normal = PvpKillExtendedRewardZones.TryResolve(38, false, PvpKillRewardZoneRuntimeState.Inactive);
        var stun = PvpKillExtendedRewardZones.TryResolve(38, true, PvpKillRewardZoneRuntimeState.Inactive);

        Assert.Equal(normal, stun);
    }

    [Theory]
    [InlineData((short)49)]
    [InlineData((short)51)]
    [InlineData((short)53)]
    [InlineData((short)160)]
    [InlineData((short)164)]
    [InlineData((short)295)]
    [InlineData((short)296)]
    public void RegularWarDropZone_GrantsFullSetUngated_EvenOnStunAndWithNoRuntime(short zoneId)
    {
        var profile = PvpKillExtendedRewardZones.TryResolve(zoneId, true, PvpKillRewardZoneRuntimeState.Inactive);

        Assert.NotNull(profile);
        Assert.Equal(FullReward, profile.Value);
    }

    [Theory]
    [InlineData((short)195)]
    [InlineData((short)196)]
    public void Map195Event_WhenActive_GrantsFullSet(short zoneId)
    {
        var profile = PvpKillExtendedRewardZones.TryResolve(zoneId, false,
            new PvpKillRewardZoneRuntimeState(Map195TimeEventActive: true));

        Assert.NotNull(profile);
        Assert.Equal(FullReward, profile.Value);
    }

    [Theory]
    [InlineData((short)195)]
    [InlineData((short)196)]
    public void Map195Event_WhenInactive_GrantsNothing(short zoneId)
    {
        var profile = PvpKillExtendedRewardZones.TryResolve(zoneId, false, PvpKillRewardZoneRuntimeState.Inactive);

        Assert.NotNull(profile);
        Assert.Equal(PvpKillZoneRewardProfile.None, profile.Value);
    }

    [Theory]
    [InlineData((short)1)] // city zone -- owned by PvpKillRewardZoneCatalog, not this extension
    [InlineData((short)140)]
    [InlineData((short)194)] // unconditional-full -- owned by PvpKillRewardZoneCatalog
    [InlineData((short)267)]
    [InlineData((short)335)] // FFA -- owned by PvpKillRewardZoneCatalog
    [InlineData((short)999)] // truly unlisted
    public void ZonesOwnedByTheBaseCatalog_ReturnNullSoTheCallerFallsThrough(short zoneId)
    {
        Assert.Null(PvpKillExtendedRewardZones.TryResolve(zoneId, false, PvpKillRewardZoneRuntimeState.Inactive));
    }
}
