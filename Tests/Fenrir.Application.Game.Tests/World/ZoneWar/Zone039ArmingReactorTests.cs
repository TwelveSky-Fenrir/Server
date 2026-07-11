using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone039ArmingReactorTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void Apply_OnServer74_InvokesTheMonsterSummonResetGateway()
    {
        var registry = CreateRegistry(74);
        var gateway = new FakeGateway();
        var reactor = new Zone039ArmingReactor(gateway);

        reactor.Apply(registry);

        Assert.Equal(new List<short> { 74 }, gateway.ResetMapIds);
    }

    [Theory]
    [InlineData((short)39)]
    [InlineData((short)144)]
    [InlineData((short)145)]
    [InlineData((short)313)]
    public void Apply_OnOtherGatedServers_DoesNotInvokeTheMonsterSummonResetGateway(short mapId)
    {
        var registry = CreateRegistry(mapId);
        var gateway = new FakeGateway();
        var reactor = new Zone039ArmingReactor(gateway);

        reactor.Apply(registry);

        Assert.Empty(gateway.ResetMapIds);
    }

    [Theory]
    [InlineData((short)39)]
    [InlineData((short)144)]
    [InlineData((short)145)]
    [InlineData((short)313)]
    [InlineData((short)74)]
    public void Apply_OnAnyGatedServer_ArmsTheVestigialBattleStateFlag(short mapId)
    {
        var registry = CreateRegistry(mapId);
        var gateway = new FakeGateway();
        var reactor = new Zone039ArmingReactor(gateway);

        reactor.Apply(registry);

        var state = reactor.TryGetState(mapId);
        Assert.NotNull(state);
        Assert.Equal(1, state.BattleState);
        Assert.Equal(0, state.PostTick);
    }

    [Fact]
    public void Apply_OnAnUngatedServer_HasNoEffectAtAll()
    {
        var registry = CreateRegistry(5);
        var gateway = new FakeGateway();
        var reactor = new Zone039ArmingReactor(gateway);

        reactor.Apply(registry);

        Assert.Empty(gateway.ResetMapIds);
        Assert.Null(reactor.TryGetState(5));
    }

    [Fact]
    public void LoggingOnlyGateway_DoesNotThrow()
    {
        var gateway = new LoggingOnlyZone039MonsterSummonResetGateway(
            NullLogger<LoggingOnlyZone039MonsterSummonResetGateway>.Instance);
        var zone = ZoneTestKit.CreateZone(74);

        var exception = Record.Exception(() => gateway.ResetGeneralSpawnTable(zone));

        Assert.Null(exception);
    }

    private sealed class FakeGateway : IZone039MonsterSummonResetGateway
    {
        public List<short> ResetMapIds { get; } = [];

        public void ResetGeneralSpawnTable(Zone zone)
        {
            ResetMapIds.Add(zone.MapId);
        }
    }
}
