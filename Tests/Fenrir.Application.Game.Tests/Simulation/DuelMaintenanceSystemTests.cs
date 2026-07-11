using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Simulation;

public class DuelMaintenanceSystemTests
{
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

        zone.ApplyDeath(20);

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.True(zone.TryGetPlayer(10, out var winner));
        Assert.True(zone.TryGetPlayer(20, out var loser));
        Assert.True(winner!.CanUseConsumables);
        Assert.True(loser!.CanUseConsumables);

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

        zone.ApplyDeath(10);

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

        zone.Tick(TimeSpan.FromMilliseconds(500));

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

        zone.Tick(TimeSpan.FromMilliseconds(500 * 179));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);
        Assert.True(duels.TryGetActiveDuel(10, out var almostExpired));
        Assert.Equal(1, almostExpired!.RemainingTicks);

        zone.Tick(TimeSpan.FromMilliseconds(500));

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

        zone.Tick(TimeSpan.FromMilliseconds(500 * 250));

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

        zone.Tick(TimeSpan.FromMilliseconds(500 * 500));

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
        Enter(zone, 30, observerSession, 101f, 101f);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        StartDuel(duels, 10, 20);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);
        ZoneTestKit.DrainOutbound(observerPipe);

        zone.ApplyDeath(20);
        zone.Tick(TimeSpan.FromMilliseconds(500));

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
