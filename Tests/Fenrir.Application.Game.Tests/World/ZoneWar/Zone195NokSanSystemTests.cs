using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone195NokSanSystemTests
{
    private const short RewardMapId = 196;
    private const short PlainMapId = 199;
    private const short WitnessMapId = 1;
    private const float PostX = 100f;
    private const float PostZ = 100f;

    private const int FullCaptureBurstTicks =
        Zone195NokSanSystem.SettleLegacyTicks + 5 * Zone195NokSanSystem.CountdownIntervalLegacyTicks;

    private static Zone195NokSanSite RewardSite()
    {
        return new Zone195NokSanSite(RewardMapId, 0, 196, PostX, PostZ);
    }

    private static Zone195NokSanSite PlainSite()
    {
        return new Zone195NokSanSite(PlainMapId, 2, 99, PostX, PostZ);
    }

    private static Zone195NokSanSystem CreateSystem(RecordingBroadcaster broadcaster, Zone195NokSanState state,
        Zone195NokSanSiteCatalog catalog, HeroRankPointAccumulator? hero = null, Func<DateTime>? utcNow = null)
    {
        return new Zone195NokSanSystem(catalog, state, new Lazy<IZone195NokSanBroadcaster>(() => broadcaster),
            hero ?? new HeroRankPointAccumulator(), NullLogger<Zone195NokSanSystem>.Instance,
            utcNow ?? MondayNoon);
    }

    private static (Zone Zone, ZoneClientSession Session, FakeDuplexPipe Pipe) EnterPlayer(ZoneRegistry registry,
        short mapId, int characterId, byte tribe, float posX = PostX, float posZ = PostZ, string name = "Hero")
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        registry[mapId].Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, mapId, name, posX, 0f, posZ, tribe: tribe)));
        registry[mapId].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        return (registry[mapId], session, pipe);
    }

    private static PlayerRuntimeState PlayerState(Zone zone, int characterId)
    {
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    private static DateTime MondayNoon()
    {
        return SundayAt(20).AddDays(1);
    }

    private static DateTime SundayInWindow()
    {
        return SundayAt(20);
    }

    private static DateTime SundayAt(int hour)
    {
        var d = new DateTime(2026, 7, 1, hour, 30, 0, DateTimeKind.Utc);
        while (d.DayOfWeek != DayOfWeek.Sunday)
            d = d.AddDays(1);
        return d;
    }

    [Fact]
    public void UnconfiguredMap_Simulate_IsANoOp()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([WitnessMapId]);
        var (zone, _, _) = EnterPlayer(registry, WitnessMapId, 1, 1);

        var broadcaster = new RecordingBroadcaster();
        var state = new Zone195NokSanState();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1000);

        Assert.Empty(broadcaster.ChallengerAppeared);
        Assert.Empty(broadcaster.Countdowns);
    }

    [Fact]
    public void EligibleChallenger_InsideRadius_LocksAndAnnouncesChallengerAppeared()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1, name: "Challenger");

        var broadcaster = new RecordingBroadcaster();
        var system = CreateSystem(broadcaster, new Zone195NokSanState(),
            new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);

        var appeared = Assert.Single(broadcaster.ChallengerAppeared);
        Assert.Equal((byte)1, appeared.Tribe);
        Assert.Equal("Challenger", appeared.Name);
        Assert.Empty(broadcaster.Countdowns);
    }

    [Fact]
    public void FullCapture_EmitsCountdownSequence_ThenSuccess_ThenState_AndFlipsStone()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);

        var broadcaster = new RecordingBroadcaster();
        var state = new Zone195NokSanState();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);
        system.Simulate(zone, FullCaptureBurstTicks);

        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, broadcaster.Countdowns.ConvertAll(c => c.Remaining));
        Assert.All(broadcaster.Countdowns, c => Assert.Equal((short)99, c.Server));

        var success = Assert.Single(broadcaster.CaptureSucceeded);
        Assert.Equal((byte)1, success.Tribe);
        Assert.Equal((short)99, success.Server);

        var stateBroadcast = Assert.Single(broadcaster.NokSanState);
        Assert.Equal((byte)1, stateBroadcast.OwningTribe);

        Assert.Equal((byte)1, state.GetOwningTribe(2));
        Assert.Equal(1, state.GetStonesHeld(1));
        Assert.Empty(broadcaster.CaptureCancelled);
    }

    [Fact]
    public void OwningTribeMember_CannotChallengeItsOwnStone()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);

        var state = new Zone195NokSanState();
        state.CommitCapture(2, 1);

        var broadcaster = new RecordingBroadcaster();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);

        Assert.Empty(broadcaster.ChallengerAppeared);
    }

    [Fact]
    public void ChallengerOutsideCaptureRadius_IsNotLocked()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1, 500, 500);

        var broadcaster = new RecordingBroadcaster();
        var system = CreateSystem(broadcaster, new Zone195NokSanState(),
            new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);

        Assert.Empty(broadcaster.ChallengerAppeared);
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(false, false, Zone195NokSanSystem.DisqualifyingActionSort)]
    public void IneligibleCandidate_IsNotLocked(bool dead, bool zoning, int actionSort)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);

        var candidate = PlayerState(zone, 1);
        candidate.IsDead = dead;
        candidate.IsMovingZone = zoning;
        candidate.ActionSort = actionSort;

        var broadcaster = new RecordingBroadcaster();
        var system = CreateSystem(broadcaster, new Zone195NokSanState(),
            new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);

        Assert.Empty(broadcaster.ChallengerAppeared);
    }

    [Fact]
    public void CapturerBecomesIneligibleMidSettle_CancelsAndResets_StoneUnchanged()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);

        var broadcaster = new RecordingBroadcaster();
        var state = new Zone195NokSanState();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);
        PlayerState(zone, 1).IsDead = true;
        system.Simulate(zone, Zone195NokSanSystem.SettleLegacyTicks);

        Assert.Single(broadcaster.CaptureCancelled);
        Assert.Empty(broadcaster.CaptureSucceeded);
        Assert.Null(state.GetOwningTribe(2));
    }

    [Fact]
    public void CapturerLeavesRadiusMidCountdown_Cancels()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);

        var broadcaster = new RecordingBroadcaster();
        var state = new Zone195NokSanState();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([PlainSite()]));

        system.Simulate(zone, 1);
        system.Simulate(zone, Zone195NokSanSystem.SettleLegacyTicks);
        Assert.Single(broadcaster.Countdowns);

        PlayerState(zone, 1).PosX = 9000;
        system.Simulate(zone, Zone195NokSanSystem.CountdownIntervalLegacyTicks);

        Assert.Single(broadcaster.CaptureCancelled);
        Assert.Empty(broadcaster.CaptureSucceeded);
        Assert.Null(state.GetOwningTribe(2));
    }

    [Fact]
    public void RewardWindowOpen_HighLevel_GrantsCapturerAndNearbyAllyCpAndHeroPoints()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([RewardMapId]);
        var (zone, _, _) = EnterPlayer(registry, RewardMapId, 1, 1);
        EnterPlayer(registry, RewardMapId, 2, 1, 150, 150);

        var capturer = PlayerState(zone, 1);
        var ally = PlayerState(zone, 2);
        capturer.Level = 130;
        ally.Level = 130;
        var capCpBefore = capturer.ContributionPoints;
        var capHeroBefore = capturer.HeroRankPoints;
        var allyCpBefore = ally.ContributionPoints;
        var allyHeroBefore = ally.HeroRankPoints;

        var broadcaster = new RecordingBroadcaster();
        var system = CreateSystem(broadcaster, new Zone195NokSanState(),
            new Zone195NokSanSiteCatalog([RewardSite()]), utcNow: SundayInWindow);

        system.Simulate(zone, 1);
        system.Simulate(zone, FullCaptureBurstTicks);

        Assert.Equal(capCpBefore + Zone195NokSanSystem.CapturerContributionPoints, capturer.ContributionPoints);
        Assert.Equal(capHeroBefore + Zone195NokSanSystem.CapturerHeroPoints, capturer.HeroRankPoints);
        Assert.Equal(allyCpBefore + Zone195NokSanSystem.AllyContributionPoints, ally.ContributionPoints);
        Assert.Equal(allyHeroBefore + Zone195NokSanSystem.AllyHeroPoints, ally.HeroRankPoints);
    }

    [Fact]
    public void RewardWindowOpen_BelowLevel113_GrantsCpButNoHeroPoints()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([RewardMapId]);
        var (zone, _, _) = EnterPlayer(registry, RewardMapId, 1, 1);

        var capturer = PlayerState(zone, 1);
        capturer.Level = 42;
        var capCpBefore = capturer.ContributionPoints;
        var capHeroBefore = capturer.HeroRankPoints;

        var broadcaster = new RecordingBroadcaster();
        var system = CreateSystem(broadcaster, new Zone195NokSanState(),
            new Zone195NokSanSiteCatalog([RewardSite()]), utcNow: SundayInWindow);

        system.Simulate(zone, 1);
        system.Simulate(zone, FullCaptureBurstTicks);

        Assert.Equal(capCpBefore + Zone195NokSanSystem.CapturerContributionPoints, capturer.ContributionPoints);
        Assert.Equal(capHeroBefore, capturer.HeroRankPoints);
    }

    [Fact]
    public void RewardWindowClosed_FlipsStoneButGrantsNoReward()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([RewardMapId]);
        var (zone, _, _) = EnterPlayer(registry, RewardMapId, 1, 1);

        var capturer = PlayerState(zone, 1);
        capturer.Level = 130;
        var capCpBefore = capturer.ContributionPoints;
        var capHeroBefore = capturer.HeroRankPoints;

        var broadcaster = new RecordingBroadcaster();
        var state = new Zone195NokSanState();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([RewardSite()]));

        system.Simulate(zone, 1);
        system.Simulate(zone, FullCaptureBurstTicks);

        Assert.Equal((byte)1, state.GetOwningTribe(0));
        Assert.Equal(capCpBefore, capturer.ContributionPoints);
        Assert.Equal(capHeroBefore, capturer.HeroRankPoints);
    }

    [Fact]
    public void NonRewardShard_EvenInsideWindow_GrantsNoReward()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId]);
        var (zone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);

        var capturer = PlayerState(zone, 1);
        capturer.Level = 130;
        var capCpBefore = capturer.ContributionPoints;
        var capHeroBefore = capturer.HeroRankPoints;

        var broadcaster = new RecordingBroadcaster();
        var state = new Zone195NokSanState();
        var system = CreateSystem(broadcaster, state, new Zone195NokSanSiteCatalog([PlainSite()]),
            utcNow: SundayInWindow);

        system.Simulate(zone, 1);
        system.Simulate(zone, FullCaptureBurstTicks);

        Assert.Equal((byte)1, state.GetOwningTribe(2));
        Assert.Equal(capCpBefore, capturer.ContributionPoints);
        Assert.Equal(capHeroBefore, capturer.HeroRankPoints);
    }

    [Fact]
    public void ChallengerAppeared_BroadcastsClusterWide_ViaConcreteBroadcaster()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([PlainMapId, WitnessMapId]);
        var (nokSanZone, _, _) = EnterPlayer(registry, PlainMapId, 1, 1);
        var (_, _, witnessPipe) = EnterPlayer(registry, WitnessMapId, 2, 3);

        var realBroadcaster = new Zone195NokSanBroadcaster(registry, NullLogger<Zone195NokSanBroadcaster>.Instance);
        var system = new Zone195NokSanSystem(new Zone195NokSanSiteCatalog([PlainSite()]), new Zone195NokSanState(),
            new Lazy<IZone195NokSanBroadcaster>(() => realBroadcaster), new HeroRankPointAccumulator(),
            NullLogger<Zone195NokSanSystem>.Instance, MondayNoon);

        system.Simulate(nokSanZone, 1);

        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 771, 1);
    }

    private static void AssertZoneEventInfo(byte[] frame, int expectedSort, int expectedFirstDataInt)
    {
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
        Assert.Equal(expectedSort, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
        Assert.Equal(expectedFirstDataInt, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5)));
    }

    private sealed class RecordingBroadcaster : IZone195NokSanBroadcaster
    {
        public List<(byte Tribe, string Name)> ChallengerAppeared { get; } = [];
        public List<short> CaptureCancelled { get; } = [];
        public List<(int Remaining, short Server)> Countdowns { get; } = [];
        public List<(byte Tribe, short Server, string Name)> CaptureSucceeded { get; } = [];
        public List<(byte OwningTribe, short Server, Zone195NokSanStateSnapshot Snapshot)> NokSanState { get; } = [];

        public void AnnounceChallengerAppeared(byte challengerTribe, string challengerName)
        {
            ChallengerAppeared.Add((challengerTribe, challengerName));
        }

        public void AnnounceCaptureCancelled(short serverNumber)
        {
            CaptureCancelled.Add(serverNumber);
        }

        public void AnnounceCountdown(int remainingTime, short serverNumber)
        {
            Countdowns.Add((remainingTime, serverNumber));
        }

        public void AnnounceCaptureSucceeded(byte winningTribe, short serverNumber, string capturerName)
        {
            CaptureSucceeded.Add((winningTribe, serverNumber, capturerName));
        }

        public void AnnounceNokSanState(byte owningTribe, short serverNumber, Zone195NokSanStateSnapshot snapshot)
        {
            NokSanState.Add((owningTribe, serverNumber, snapshot));
        }
    }
}
