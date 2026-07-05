using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Application.Game.World.Monsters;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers <see cref="MonsterSpawnScheduler.ProcessDeath" />'s party-loot-share wiring: a partied killer's
///     drop must carry DropSort=1 and the party's own name (the leader's character name -- legacy's
///     <c>aPartyName</c>, <c>S04_MyWork02.cpp:9720-9721</c>) instead of the previously-hardcoded
///     <c>PartyName=""/DropSort=0</c>, so <see cref="GroundItemEntity.IsClaimableBy" />'s party-share window
///     actually becomes reachable.
/// </summary>
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
        Assert.Equal(new[] { 10, 11 }, parties.GetMembers(10)); // leader (10, "Leader") first

        var zone = CreateZoneWithGuaranteedDrop(parties);
        var (leaderSession, _) = ZoneTestKit.CreateSession(1);
        var (mateSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(leaderSession, 1, "Leader", 0, posZ: 0, level: 1)));
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(mateSession, 1, "Mate", 50, posZ: 50, level: 1)));
        zone.Tick(SimulationClock.LegacyTick); // enters + pops the monster

        // The non-leader member lands the kill -- PartyName must resolve to the *leader's* name, not the killer's own.
        zone.TryDamageMonster(1, 10_000, 11, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick); // drains the death: rolls the guaranteed potion drop
        Assert.Equal(1, zone.GroundItemCount);

        // Too early (before the 10s party-share delay) -- not the killer, not expired, not yet party-shareable.
        var tooEarly = zone.TryClaimGroundItem(1, 1u, "Leader", "Leader", 50, 0, 50, out _);
        Assert.Equal(GroundItemClaimOutcome.NotOwned, tooEarly);

        // Wrong party name -- never claimable through the party-share branch, no matter how long it waits.
        for (var i = 0; i < 25; i++) // 12.5s -- past the party-share delay, still short of free-for-all
            zone.Tick(SimulationClock.LegacyTick);
        var wrongParty = zone.TryClaimGroundItem(1, 1u, "Stranger", "SomeoneElse", 50, 0, 50, out _);
        Assert.Equal(GroundItemClaimOutcome.NotOwned, wrongParty);

        // Correct party name, correct member, after the share delay -- now claimable.
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

        // Past the party-share delay: a solo drop has no party name to match, so this must still fail --
        // only the killer (or the free-for-all window) can claim it.
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
