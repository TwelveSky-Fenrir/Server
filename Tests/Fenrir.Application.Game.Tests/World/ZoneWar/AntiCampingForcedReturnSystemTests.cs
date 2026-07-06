using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="AntiCampingForcedReturnSystem" /> (FIX_HSB_POS_BUG). Every test drives the system
///     directly via <see cref="AntiCampingForcedReturnSystem.Simulate" /> rather than through
///     <see cref="Zone.Tick" />'s full pipeline, so the unrelated 3.5 s avatar-rebroadcast keep-alive never
///     interleaves with the forced-return notification on the same fake pipe.
/// </summary>
public class AntiCampingForcedReturnSystemTests
{
    private const short GuardedMapId = 2; // one of AntiCampingGuardPointCatalog.GuardedMapIds
    private const short UnguardedMapId = 999;

    private static AntiCampingGuardPointCatalog CatalogWithSymbolPoints(short mapId,
        params AntiCampingGuardPoint[] points)
    {
        var byMap = new Dictionary<short, AntiCampingMapGuardPoints>
        {
            [mapId] = new([..points], null)
        };
        return new AntiCampingGuardPointCatalog(byMap);
    }

    private static AntiCampingGuardPointCatalog CatalogWithSymbolAndTower(short mapId,
        AntiCampingGuardPoint[] symbolPoints, AntiCampingGuardPoint tower)
    {
        var byMap = new Dictionary<short, AntiCampingMapGuardPoints>
        {
            [mapId] = new([..symbolPoints], tower)
        };
        return new AntiCampingGuardPointCatalog(byMap);
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) EnterPlayer(short mapId,
        float posX = 100f, float posY = 0f, float posZ = 100f)
    {
        var zone = ZoneTestKit.CreateZone(mapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId, posX: posX, posY: posY, posZ: posZ)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // just drains the Enter command; too short to simulate

        Assert.True(zone.TryGetPlayer(10, out var state));
        ZoneTestKit.DrainOutbound(pipe); // discard anything (none expected for a lone occupant, but be safe)

        return (zone, state!, pipe);
    }

    [Fact]
    public void UnguardedMap_StandingExactlyOnAConfiguredPoint_CounterNeverTouched_NoNotification()
    {
        var catalog = CatalogWithSymbolPoints(UnguardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, pipe) = EnterPlayer(UnguardedMapId);

        for (var i = 0; i < 25; i++)
            system.Simulate(zone, 1);

        Assert.Equal(0, state.AntiCampingProximityCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void GuardedMapWithNoConfiguredPoints_UsesEmptyCatalog_CounterNeverTouched()
    {
        var system = new AntiCampingForcedReturnSystem(AntiCampingGuardPointCatalog.Empty);
        var (zone, state, pipe) = EnterPlayer(GuardedMapId);

        for (var i = 0; i < 25; i++)
            system.Simulate(zone, 1);

        Assert.Equal(0, state.AntiCampingProximityCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void WithinRadiusOfSymbolPoint_IncrementsCounterOncePerQualifyingTick()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId);

        system.Simulate(zone, 1);
        Assert.Equal(1, state.AntiCampingProximityCounter);

        system.Simulate(zone, 1);
        Assert.Equal(2, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void ExactlyAtRadiusBoundary_CountsAsInRange()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(0, 0, 0));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId, AntiCampingForcedReturnSystem.ProximityRadius, 0, 0);

        system.Simulate(zone, 1);

        Assert.Equal(1, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void JustOutsideRadius_NeverIncrements()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(0, 0, 0));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) =
            EnterPlayer(GuardedMapId, AntiCampingForcedReturnSystem.ProximityRadius + 0.1f, 0, 0);

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void VerticalSeparationAlone_TakesTheAvatarOutOfRange()
    {
        // Same X/Z as the guarded point, but far enough on Y alone that full 3-axis distance exceeds the
        // radius -- proves the check is NOT horizontal-only (GetLengthXYZ, mapcheck.h:12-15).
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId, 100, 1000);

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void OutOfRangeAfterBuildingUpCounter_ResetsToZero()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId);

        system.Simulate(zone, 1);
        system.Simulate(zone, 1);
        Assert.Equal(2, state.AntiCampingProximityCounter);

        state.PosX = 100_000f;
        system.Simulate(zone, 1);

        Assert.Equal(0, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void MultiplePoints_OnlyTheLastEvaluatedPointsOutcomeSurvives_EvenStandingOnAnEarlierOne()
    {
        var near = new AntiCampingGuardPoint(100, 0, 100);
        var far = new AntiCampingGuardPoint(100_000, 0, 100_000);
        var catalog = CatalogWithSymbolPoints(GuardedMapId, near, far); // near evaluated first, far last
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId); // standing on `near`, nowhere near `far`

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void MultiplePoints_LastEvaluatedInRange_Increments()
    {
        var far = new AntiCampingGuardPoint(100_000, 0, 100_000);
        var near = new AntiCampingGuardPoint(100, 0, 100);
        var catalog = CatalogWithSymbolPoints(GuardedMapId, far, near); // far evaluated first, near last
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId);

        system.Simulate(zone, 1);

        Assert.Equal(1, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void TowerCheck_SkippedWhenASymbolPointAlreadyFlagged_CounterNotResetByOutOfRangeTower()
    {
        var symbolNear = new AntiCampingGuardPoint(100, 0, 100);
        var towerFar = new AntiCampingGuardPoint(100_000, 0, 100_000);
        var catalog = CatalogWithSymbolAndTower(GuardedMapId, [symbolNear], towerFar);
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId);

        system.Simulate(zone, 1);

        // If the Tower check ran despite the symbol flag already being set, this would have been reset to 0.
        Assert.Equal(1, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void TowerCheck_RunsWhenNoSymbolPointFlaggedThisTick()
    {
        var symbolFar = new AntiCampingGuardPoint(100_000, 0, 100_000);
        var towerNear = new AntiCampingGuardPoint(100, 0, 100);
        var catalog = CatalogWithSymbolAndTower(GuardedMapId, [symbolFar], towerNear);
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, _) = EnterPlayer(GuardedMapId);

        system.Simulate(zone, 1);

        Assert.Equal(1, state.AntiCampingProximityCounter);
    }

    [Fact]
    public void ThresholdReached_SendsForcedReturnNotification_NotBeforeThreshold()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, pipe) = EnterPlayer(GuardedMapId);

        for (var i = 0; i < AntiCampingForcedReturnSystem.ForcedReturnThreshold - 1; i++)
        {
            system.Simulate(zone, 1);
            Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        }

        system.Simulate(zone, 1); // the 20th consecutive qualifying tick
        Assert.Equal(AntiCampingForcedReturnSystem.ForcedReturnThreshold, state.AntiCampingProximityCounter);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expectedFrame = new byte[FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), expectedFrame);
        Assert.Equal(expectedFrame, sent);
    }

    [Fact]
    public void ThresholdReached_RepeatsEveryTickWithNoCooldown_WhileStillInRange()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, pipe) = EnterPlayer(GuardedMapId);

        for (var i = 0; i < AntiCampingForcedReturnSystem.ForcedReturnThreshold; i++)
            system.Simulate(zone, 1);
        ZoneTestKit.DrainOutbound(pipe);

        system.Simulate(zone, 1);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));

        Assert.True(state.AntiCampingProximityCounter > AntiCampingForcedReturnSystem.ForcedReturnThreshold);
    }

    [Fact]
    public void BurstOfManyLegacyTicks_CatchesUpTheWholeAmountAtOnce_ReachingThresholdImmediately()
    {
        var catalog = CatalogWithSymbolPoints(GuardedMapId, new AntiCampingGuardPoint(100, 0, 100));
        var system = new AntiCampingForcedReturnSystem(catalog);
        var (zone, state, pipe) = EnterPlayer(GuardedMapId);

        system.Simulate(zone, AntiCampingForcedReturnSystem.ForcedReturnThreshold);

        Assert.Equal(AntiCampingForcedReturnSystem.ForcedReturnThreshold, state.AntiCampingProximityCounter);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }
}
