using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class RankBuffServiceTests
{
    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Fact]
    public void Sort1_DefaultStoneCount_HealsAndMirrorsRankBuffType()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;

        var service = new RankBuffService(ZoneTestKit.CreateWorldState());
        var result = service.Apply(zone, state, 10, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(result.Succeeded);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(1, player!.RankBuffType);
        Assert.Equal(800, player.Life);
        Assert.Equal(300, player.Mana);
    }

    [Fact]
    public void Sort2_InsufficientDefaultStoneCount_Fails()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new RankBuffService(ZoneTestKit.CreateWorldState());

        var result = service.Apply(zone, state, 10, 2);

        Assert.False(result.Succeeded);
        Assert.False(result.SilentlyIgnored);
    }

    [Fact]
    public void OutOfRangeSort_Fails()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new RankBuffService(ZoneTestKit.CreateWorldState());

        var result = service.Apply(zone, state, 10, 8);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void IsMovingZone_Fails_EvenWithOtherwiseValidTier()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.IsMovingZone = true;
        var service = new RankBuffService(ZoneTestKit.CreateWorldState());

        var result = service.Apply(zone, state, 10, 1);

        Assert.False(result.Succeeded);
        Assert.False(result.SilentlyIgnored);
        Assert.Equal(RankBuffResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void WorldTribeSymbolBattleActive_SilentlyIgnored_EvenWithOtherwiseValidTier()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var repository = new FakeWorldStateRepository
        {
            Row = new(Id: 1, Zone038WinTribe: null, Zone038WinTribeTime: 0, TribeSymbolBattle: true,
                MonsterSymbol: null, MonsterSymbolEndTime: 0, HighTribe: null, UpdateTribePoint: 0,
                UpdatedAtUtc: DateTime.UtcNow)
        };
        var service = new RankBuffService(ZoneTestKit.CreateWorldState(repository));

        var result = service.Apply(zone, state, 10, 1);

        Assert.False(result.Succeeded);
        Assert.True(result.SilentlyIgnored);
        Assert.Equal(RankBuffResolver.Outcome.WorldBattleActive, result.Outcome);
    }
}
