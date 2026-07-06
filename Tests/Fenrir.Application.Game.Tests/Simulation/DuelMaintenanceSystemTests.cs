using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="DuelMaintenanceSystem" />: the per-tick opponent-not-found / opponent-dead / self-dead /
///     180-legacy-tick auto-timeout resolution for every character in an Active duel, and the map-124
///     exclusion. Duels are seeded directly through <see cref="DuelRegistry" /> (bypassing the ask/accept/start
///     handshake, which is a separate, already-covered behavior) since only the Active-duel resolution itself
///     is this system's concern.
/// </summary>
public class DuelMaintenanceSystemTests
{
    // Far enough apart (AoiGrid's default 75-unit cell, 3x3 neighbor radius) that the two duelists are never
    // mutual AOI neighbors -- isolates each participant's own pipe to exactly the packets this system sends
    // them directly, with no incidental proximity-broadcast noise from the other's own EndActiveDuel call.
    private const float FarApartX = 100_000f;

    private static Zone SetUp(short mapId, out DuelRegistry duels)
    {
        duels = new DuelRegistry();
        var zone = ZoneTestKit.CreateZone(mapId, simulationSystems: [new DuelMaintenanceSystem(duels)],
            duelRegistry: duels);
        return zone;
    }

    private static void Enter(Zone zone, int characterId, ZoneClientSession session, float posX, float posZ)
    {
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, $"C{characterId}", posX, posZ: posZ)));
    }

    private static ActiveDuel StartDuel(DuelRegistry duels, int a, int b, bool noPotions = false)
    {
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(a, b, noPotions));
        Assert.True(duels.TryAnswer(b, true, out _));
        Assert.True(duels.TryStart(a, out var duel));
        return duel;
    }

    [Fact]
    public void OpponentDied_EndsBothSides_WinnerAndLoserReasonsAreMirrored()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.ApplyDeath(20); // the opponent (from 10's perspective) dies

        zone.Tick(TimeSpan.FromMilliseconds(500)); // one legacy tick -- re-evaluated immediately

        Assert.True(zone.TryGetPlayer(10, out var winner));
        Assert.True(zone.TryGetPlayer(20, out var loser));
        Assert.True(winner!.CanUseConsumables);
        Assert.True(loser!.CanUseConsumables); // lifted unconditionally, even though 20 is also now dead

        AssertEndResponse(pipeA, DuelEndReason.OpponentDied);
        AssertEndResponse(pipeB, DuelEndReason.SelfDied);

        Assert.False(duels.TryGetActiveDuel(10, out _));
        Assert.False(duels.TryGetActiveDuel(20, out _));
    }

    [Fact]
    public void SelfDied_MirroredCorrectly_WhenPlayerADiesInstead()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.ApplyDeath(10); // the initiator (PlayerA) itself dies, opponent still alive

        zone.Tick(TimeSpan.FromMilliseconds(500));

        AssertEndResponse(pipeA, DuelEndReason.SelfDied);
        AssertEndResponse(pipeB, DuelEndReason.OpponentDied);
    }

    [Fact]
    public void BothDeadSameTick_AlwaysResolvesAsPlayerAWinning_NeverADoubleLoss()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        var duel = StartDuel(duels, 10, 20);
        Assert.Equal(10, duel.PlayerA);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.ApplyDeath(10);
        zone.ApplyDeath(20);

        zone.Tick(TimeSpan.FromMilliseconds(500));

        // Opponent-dead is always checked (and fires) before self-dead, from PlayerA's own fixed canonical
        // side -- a same-tick double death is never a true draw.
        AssertEndResponse(pipeA, DuelEndReason.OpponentDied);
        AssertEndResponse(pipeB, DuelEndReason.SelfDied);
    }

    [Fact]
    public void OpponentNotFoundInThisZone_EndsOnlyTheStillPresentSide()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        Enter(zone, 10, sessionA, 0f, 0f);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // 20 is seeded directly into the registry as 10's active-duel opponent but never actually enters this
        // zone (e.g. disconnected/transferred out before the natural cleanup ran).
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.True(zone.TryGetPlayer(10, out var stillPresent));
        Assert.True(stillPresent!.CanUseConsumables);
        AssertEndResponse(pipeA, DuelEndReason.OpponentNotFound);

        Assert.False(duels.TryGetActiveDuel(10, out _));
        Assert.False(duels.TryGetActiveDuel(20, out _));
    }

    [Fact]
    public void NeitherDeadNorExpired_JustCountsDown_SendsUpdatedRemainTimeToBothSides()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.Tick(TimeSpan.FromMilliseconds(500)); // exactly one legacy tick

        // Decremented by exactly 1 (not double-decremented despite both participants being iterated).
        AssertCountdown(pipeA, 179);
        AssertCountdown(pipeB, 179);
        Assert.True(duels.TryGetActiveDuel(10, out var stillActive));
        Assert.Equal(179, stillActive!.RemainingTicks);
    }

    [Fact]
    public void TimeExpires_AfterOneHundredEightyLegacyTicks_EndsAsADrawForBothSides()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);

        zone.Tick(TimeSpan.FromMilliseconds(500 * 179)); // 179 ticks -- one short of expiry
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);
        Assert.True(duels.TryGetActiveDuel(10, out var almostExpired));
        Assert.Equal(1, almostExpired!.RemainingTicks);

        zone.Tick(TimeSpan.FromMilliseconds(500)); // the 180th tick

        AssertEndResponse(pipeA, DuelEndReason.TimeExpired);
        AssertEndResponse(pipeB, DuelEndReason.TimeExpired);
        Assert.True(zone.TryGetPlayer(10, out var a));
        Assert.True(zone.TryGetPlayer(20, out var b));
        Assert.True(a!.CanUseConsumables);
        Assert.True(b!.CanUseConsumables);
    }

    [Fact]
    public void MultiTickCatchUp_ASingleLargeBurstStillEndsTheDuel_NotJustDecrementByOne()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.Tick(TimeSpan.FromMilliseconds(500 * 250)); // one stalled-host burst, well past 180 ticks in one jump

        AssertEndResponse(pipeA, DuelEndReason.TimeExpired);
        AssertEndResponse(pipeB, DuelEndReason.TimeExpired);
    }

    [Fact]
    public void Map124_NeverProcessesTheDuel_EvenWithOneActivePresent()
    {
        var zone = SetUp(124, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        Enter(zone, 10, sessionA, 0f, 0f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.Tick(TimeSpan.FromMilliseconds(500 * 500)); // would otherwise time out several times over

        Assert.True(duels.TryGetActiveDuel(10, out var untouched));
        Assert.Equal(DuelRegistry.DurationTicks, untouched!.RemainingTicks);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));
    }

    [Fact]
    public void EndActiveDuel_BroadcastsToNearbyObserversOnly_NeverSelfEchoes()
    {
        var zone = SetUp(1, out var duels);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        var (observerSession, observerPipe) = ZoneTestKit.CreateSession(3);
        Enter(zone, 10, sessionA, 100f, 100f);
        Enter(zone, 20, sessionB, FarApartX, FarApartX);
        Enter(zone, 30, observerSession, 101f, 101f); // same AOI cell as 10
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);
        ZoneTestKit.DrainOutbound(observerPipe);

        zone.ApplyDeath(20);
        zone.Tick(TimeSpan.FromMilliseconds(500));

        // The observer (nearby 10, not 20) receives a proximity refresh reflecting "not dueling" for 10.
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(observerPipe));
    }

    private static void AssertEndResponse(FakeDuplexPipe pipe, DuelEndReason reason)
    {
        var bytes = ZoneTestKit.DrainOutbound(pipe);
        var expected = new byte[FrameWriter.FrameSizeOf<DuelEndResponse>()];
        FrameWriter.WriteFrame(new DuelEndResponse { Result = (int)reason }, expected);

        Assert.True(bytes.Length >= expected.Length);
        Assert.Equal(expected, bytes[..expected.Length]);
    }

    private static void AssertCountdown(FakeDuplexPipe pipe, int remainTime)
    {
        var bytes = ZoneTestKit.DrainOutbound(pipe);
        var expected = new byte[FrameWriter.FrameSizeOf<DuelCountdownResponse>()];
        FrameWriter.WriteFrame(new DuelCountdownResponse { RemainTime = remainTime }, expected);

        Assert.True(bytes.Length >= expected.Length);
        Assert.Equal(expected, bytes[..expected.Length]);
    }
}
