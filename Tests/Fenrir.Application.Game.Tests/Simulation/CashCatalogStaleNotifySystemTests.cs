using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Simulation;

public class CashCatalogStaleNotifySystemTests
{
    private static readonly int InvalidatedFrameSize = FrameWriter.FrameSizeOf<CashCatalogInvalidatedResponse>();

    private static async Task<CommerceCatalogCache> CacheAtVersionAsync(int version)
    {
        var cache = new CommerceCatalogCache(NullLogger<CommerceCatalogCache>.Instance);
        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts = [new ItemMallProductRowDto(100000, 5, version, 0, 0, true)]
        };
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);
        return cache;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) EnterWorld(CommerceCatalogCache cache)
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new CashCatalogStaleNotifySystem(cache)]);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public async Task Simulate_FreshSessionSentinel_IsNeverNotified_EvenWhenVersionsDiffer()
    {
        var cache = await CacheAtVersionAsync(7);
        var (zone, state, pipe) = EnterWorld(cache);

        Assert.Equal(PlayerRuntimeState.CashCatalogVersionUnknown, state.KnownCashCatalogVersion);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(PlayerRuntimeState.CashCatalogVersionUnknown, state.KnownCashCatalogVersion);
    }

    [Fact]
    public async Task Simulate_KnownVersionMatchesLive_IsNotNotified()
    {
        var cache = await CacheAtVersionAsync(7);
        var (zone, state, pipe) = EnterWorld(cache);
        state.KnownCashCatalogVersion = 7;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(7, state.KnownCashCatalogVersion);
    }

    [Fact]
    public async Task Simulate_KnownVersionStale_SendsTheEmptyInvalidationSignal_AndResetsToTheSentinel()
    {
        var cache = await CacheAtVersionAsync(7);
        var (zone, state, pipe) = EnterWorld(cache);
        state.KnownCashCatalogVersion = 3;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(InvalidatedFrameSize, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(PlayerRuntimeState.CashCatalogVersionUnknown, state.KnownCashCatalogVersion);
    }

    [Fact]
    public async Task Simulate_AfterANotify_DoesNotReNotifyOnTheNextTick_EvenThoughVersionsStillDiffer()
    {
        var cache = await CacheAtVersionAsync(7);
        var (zone, state, pipe) = EnterWorld(cache);
        state.KnownCashCatalogVersion = 3;

        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task Simulate_ReRequestingAfterNotify_MakesTheSessionEligibleForAnotherNotifyLater()
    {
        var cache = await CacheAtVersionAsync(7);
        var (zone, state, pipe) = EnterWorld(cache);
        state.KnownCashCatalogVersion = 3;
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        state.KnownCashCatalogVersion = 7;

        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts = [new ItemMallProductRowDto(100000, 5, 8, 0, 0, true)]
        };
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(InvalidatedFrameSize, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(PlayerRuntimeState.CashCatalogVersionUnknown, state.KnownCashCatalogVersion);
    }
}
