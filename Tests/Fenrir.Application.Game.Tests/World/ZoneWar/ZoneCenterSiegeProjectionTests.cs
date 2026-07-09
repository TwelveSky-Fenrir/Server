using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="ZoneCenterSiegeProjection" />: the WorldInfo fields backed by <see cref="ZoneCenterSiegeState" />
///     /
///     <see cref="TribeGuardCorridorState" /> (Zone175/Zone267/Zone241/Zone335, the per-tribe Zone038 DTM value, the
///     three tribe bonus-ratio arrays, kill-other-tribe bonus, and TribeGuardState) must reflect those models' live
///     values instead of the zeroed template, while every other WorldInfo field passes through unchanged.
/// </summary>
public class ZoneCenterSiegeProjectionTests
{
    [Fact]
    public void Apply_FreshState_LeavesEveryProjectedFieldAtItsTemplateDefault()
    {
        var siege = new ZoneCenterSiegeState();
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        Assert.Equal(new int[13], result.Zone049TypeState);
        Assert.Equal(new int[13], result.Zone049TypeStateTime);
        Assert.Equal(new int[32], result.Zone175TypeState);
        Assert.Equal(new int[4], result.Zone267TypeState);
        Assert.Equal(new int[20], result.Zone241TypeState);
        Assert.Equal(new int[4], result.Zone038DTMValue);
        Assert.Equal(new float[4], result.TribeGeneralExperienceUpRatioInfo);
        Assert.Equal(new float[4], result.TribeItemDropUpRatioInfo);
        Assert.Equal(new float[4], result.TribeItemDropUpRatioForMyoungInfo);
        Assert.Equal(new int[4], result.TribeKillOtherTribeAddValueInfo);
        Assert.Equal(0, result.ZoneFFATypeState);
        // TribeGuardCorridorState's own boot default is fully closed (false), which is itself 0 on the wire --
        // identical to the zeroed template, but for a different reason (a real "closed" reading, not "unbacked").
        Assert.Equal(new int[16], result.TribeGuardState);

        // Untouched fields must still be the template's own zeroed defaults, not silently overwritten.
        Assert.Equal(WorldStateTemplates.ZeroedWorldInfo.TribeCloseInfo, result.TribeCloseInfo);
        Assert.Equal(WorldStateTemplates.ZeroedWorldInfo.GuildName1, result.GuildName1);
    }

    [Fact]
    public void Apply_Zone049Written_ReflectsStateAndStampedTime()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone049State(3, 5, true);
        siege.SetZone049State(7, 1, false);
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        Assert.Equal(5, result.Zone049TypeState[3]);
        Assert.NotEqual(0, result.Zone049TypeStateTime[3]);
        Assert.Equal(1, result.Zone049TypeState[7]);
        Assert.Equal(0, result.Zone049TypeStateTime[7]);
        Assert.Equal(0, result.Zone049TypeState[0]);
    }

    [Fact]
    public void Apply_Zone175Written_ReflectsTheFlattenedGrid()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone175(2, 5, 77);
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        // Row-major int[4][8]: instance 2, slot 5 -> index 2*8+5 = 21.
        Assert.Equal(77, result.Zone175TypeState[21]);
        Assert.Equal(0, result.Zone175TypeState[20]);
        Assert.Equal(0, result.Zone175TypeState[13]);
    }

    [Fact]
    public void Apply_Zone267AndZone038DtmWritten_ReflectPerTribeValues()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone267(1, 405);
        siege.SetZone038DtmValue(3, 12);
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        Assert.Equal([0, 405, 0, 0], result.Zone267TypeState);
        Assert.Equal([0, 0, 0, 12], result.Zone038DTMValue);
    }

    [Fact]
    public void Apply_Zone335Written_ReflectsTheSharedScalar()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone335(1503);
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        Assert.Equal(1503, result.ZoneFFATypeState);
    }

    [Fact]
    public void Apply_Zone241Written_ReflectsTheChallengeState()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone241(4, DenOfRebirthChallengeState.ChallengeStarted);
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        Assert.Equal((int)DenOfRebirthChallengeState.ChallengeStarted, result.Zone241TypeState[4]);
        Assert.Equal(0, result.Zone241TypeState[3]);
    }

    [Fact]
    public void Apply_TribeBonusRatiosWritten_ReflectPerTribeValues()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetExperienceBonusRatio(0, 1.5f);
        siege.SetItemDropBonusRatio(1, 2.0f);
        siege.SetMyoungItemDropBonusRatio(2, 0.5f);
        siege.SetKillOtherTribeBonus(3, 10);
        var tribeGuard = new TribeGuardCorridorState();

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        Assert.Equal([1.5f, 0f, 0f, 0f], result.TribeGeneralExperienceUpRatioInfo);
        Assert.Equal([0f, 2.0f, 0f, 0f], result.TribeItemDropUpRatioInfo);
        Assert.Equal([0f, 0f, 0.5f, 0f], result.TribeItemDropUpRatioForMyoungInfo);
        // SetKillOtherTribeBonus's own documented atomic-write shape: setting tribe 3 zeroes the other three.
        Assert.Equal([0, 0, 0, 10], result.TribeKillOtherTribeAddValueInfo);
    }

    [Fact]
    public void Apply_TribeGuardSegmentOpened_ReflectsTheFlattenedTable()
    {
        var siege = new ZoneCenterSiegeState();
        var tribeGuard = new TribeGuardCorridorState();
        tribeGuard.TrySetOpen(2, 1, true);

        var result = ZoneCenterSiegeProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, siege, tribeGuard);

        // Row-major int[4][4]: tribe 2, segment 1 -> index 2*4+1 = 9.
        Assert.Equal(1, result.TribeGuardState[9]);
        Assert.Equal(0, result.TribeGuardState[8]);
        Assert.Equal(0, result.TribeGuardState[5]);
    }

    [Fact]
    public void Apply_ComposesWithGuildRankingAndWorldStateProjections_WithoutClobberingEitherOverlay()
    {
        var siege = new ZoneCenterSiegeState();
        siege.SetZone335(1502);
        var tribeGuard = new TribeGuardCorridorState();

        var withGuildOverlay = WorldStateTemplates.ZeroedWorldInfo with { GuildName1 = "SomeGuild" };
        var result = ZoneCenterSiegeProjection.Apply(withGuildOverlay, siege, tribeGuard);

        Assert.Equal("SomeGuild", result.GuildName1);
        Assert.Equal(1502, result.ZoneFFATypeState);
    }
}
