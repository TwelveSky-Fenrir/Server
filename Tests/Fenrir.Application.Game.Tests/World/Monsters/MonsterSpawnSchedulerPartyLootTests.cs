using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterSpawnSchedulerPartyLootTests
{
    private const int PotionItemId = 8001;

    private static Zone CreateZoneWithGuaranteedDrop(PartyRegistry? parties = null)
    {
        var monster = WorldDataTestRows.Monster(900) with
        {
            Life = 10,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 900) with
        {
            Number = 1,
            LocationX = 50,
            LocationY = 0,
            LocationZ = 50,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster],
            MonsterSpawnRegions = [region],
            MonsterDropPotions = [new MonsterDropPotionRowDto(900, 0, 1_000_000, PotionItemId)]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache, static () => new MaxValueRandom(), parties);
        return ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);
    }

    [Fact]
    public void PartiedKiller_DropCarriesTheLeadersName_AndBecomesPartyClaimableAfterTheShareDelay()
    {
        var parties = new PartyRegistry();
        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(10, 1, 0, 11, 1, 0));
        Assert.True(parties.TryAnswer(11, true, out _, out _));
        Assert.Equal(new[] { 10, 11 }, parties.GetMembers(10));

        var zone = CreateZoneWithGuaranteedDrop(parties);
        var (leaderSession, _) = ZoneTestKit.CreateSession(1);
        var (mateSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(leaderSession, 1, "Leader", 0, posZ: 0, level: 1)));
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(mateSession, 1, "Mate", 50, posZ: 50, level: 1)));
        zone.Tick(SimulationClock.LegacyTick);

        zone.TryDamageMonster(1, 10_000, 11, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.GroundItemCount);

        var tooEarly = zone.TryClaimGroundItem(1, 1u, "Leader", "Leader", 50, 0, 50, out _);
        Assert.Equal(GroundItemClaimOutcome.NotOwned, tooEarly);

        for (var i = 0; i < 25; i++)
            zone.Tick(SimulationClock.LegacyTick);
        var wrongParty = zone.TryClaimGroundItem(1, 1u, "Stranger", "SomeoneElse", 50, 0, 50, out _);
        Assert.Equal(GroundItemClaimOutcome.NotOwned, wrongParty);

        var shared = zone.TryClaimGroundItem(1, 1u, "Leader", "Leader", 50, 0, 50, out var item);
        Assert.Equal(GroundItemClaimOutcome.Success, shared);
        Assert.Equal(PotionItemId, item!.ItemId);
    }

    [Fact]
    public void SoloKiller_DropCarriesNoPartyName_AndDropSort0()
    {
        var zone = CreateZoneWithGuaranteedDrop();
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, 1, "Solo", 50, posZ: 50, level: 1)));
        zone.Tick(SimulationClock.LegacyTick);

        zone.TryDamageMonster(1, 10_000, 20, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.GroundItemCount);

        for (var i = 0; i < 25; i++)
            zone.Tick(SimulationClock.LegacyTick);
        var strangerAttempt = zone.TryClaimGroundItem(1, 1u, "Stranger", "AnyPartyName", 50, 0, 50, out _);
        Assert.Equal(GroundItemClaimOutcome.NotOwned, strangerAttempt);

        var killerAttempt = zone.TryClaimGroundItem(1, 1u, "Solo", null, 50, 0, 50, out var item);
        Assert.Equal(GroundItemClaimOutcome.Success, killerAttempt);
        Assert.Equal(PotionItemId, item!.ItemId);
    }

    private sealed class MaxValueRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return Math.Max(minValue, maxValue - 1);
        }
    }
}
