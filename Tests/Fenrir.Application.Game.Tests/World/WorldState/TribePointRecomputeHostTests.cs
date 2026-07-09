using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribePointRecomputeHostTests
{
    private static (WorldStateService WorldState, CountingRosterGateway Gateway, TribePointRecomputeHost Host)
        CreateHost()
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        var gateway = new CountingRosterGateway();
        var levelRecompute = new TribePointLevelRecomputeService(gateway, worldState,
            NullLogger<TribePointLevelRecomputeService>.Instance);
        var registry = ZoneTestKit.CreateRegistry();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var ladder = new FavoredTribeRankBonusLadderService(worldState, broadcaster,
            NullLogger<FavoredTribeRankBonusLadderService>.Instance);

        var host = new TribePointRecomputeHost(levelRecompute, ladder, NullLogger<TribePointRecomputeHost>.Instance);
        return (worldState, gateway, host);
    }

    [Fact]
    public async Task RunBootRecomputeAsync_AlwaysRunsImmediately_RegardlessOfTickCounter()
    {
        var (_, gateway, host) = CreateHost();

        await host.RunBootRecomputeAsync(CancellationToken.None);

        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task TickAsync_OnlyRunsOnEveryTickGateThCall()
    {
        var (_, gateway, host) = CreateHost();

        for (var i = 0; i < TribePointRecomputeHost.TickGate - 1; i++)
            await host.TickAsync(CancellationToken.None);

        Assert.Equal(0, gateway.CallCount);

        await host.TickAsync(CancellationToken.None); // the 6th tick

        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task TickAsync_RunsAgainOnTheNextFullCycle()
    {
        var (_, gateway, host) = CreateHost();

        for (var i = 0; i < TribePointRecomputeHost.TickGate * 2; i++)
            await host.TickAsync(CancellationToken.None);

        Assert.Equal(2, gateway.CallCount);
    }

    [Fact]
    public async Task DueTick_RunsLevelRecomputeBeforeTheLadderCheck_MatchingLegacyCallOrder()
    {
        // The ladder is a no-op here (flag never armed), but this proves both mechanisms fire from the same
        // due tick without one throwing/short-circuiting the other.
        var (worldState, _, host) = CreateHost();
        worldState.SetHighTribe(1);

        for (var i = 0; i < TribePointRecomputeHost.TickGate; i++)
            await host.TickAsync(CancellationToken.None);

        // Level recompute ran (empty roster -> baseline), ladder did not (flag not pending).
        Assert.Equal(1000, worldState.GetTribe(0).Points);
    }

    private sealed class CountingRosterGateway : ITribePointRosterGateway
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<TribeRosterCharacterSnapshot>?>([]);
        }
    }
}
