using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     End-to-end wiring check: a boss kill drained through <c>MonsterSpawnScheduler.ProcessDeath</c> actually
///     spawns the <see cref="BossDropCatalog" /> item on the ground, proving the catalog reaches a real drop
///     (not just the resolver in isolation). Uses identifier 731 (Santa), whose guaranteed item 536 is a fixed
///     drop with no RNG dependence and which falls through to the generic tiers -- configured with no generic
///     drop tables here so the only resulting ground item is the boss drop itself.
/// </summary>
public class BossDropKillIntegrationTests
{
    private const int SantaGiftItemId = 536;

    [Fact]
    public void KillingSanta731_SpawnsItsGuaranteedItem536OnTheGround()
    {
        var monster = WorldDataTestRows.Monster(BossEventDropResolver.SantaMonsterId) with
        {
            Life = 10,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, BossEventDropResolver.SantaMonsterId) with
        {
            Number = 1, LocationX = 50, LocationY = 0, LocationZ = 50, Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster], MonsterSpawnRegions = [region]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        // MaxValueRandom keeps the unconditional item-864 roll unreachable, so the boss 536 is the sole drop.
        var scheduler = new MonsterSpawnScheduler(cache, static () => new MaxValueRandom());
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        const int killerCharacterId = 20;
        zone.Post(ZoneCommand.Enter(killerCharacterId,
            ZoneTestKit.EnterData(session, 1, "Looter", 50, posZ: 50, level: 1)));
        zone.Tick(SimulationClock.LegacyTick); // enters + pops the monster

        Assert.True(zone.TryGetMonster(1, out _));
        zone.TryDamageMonster(1, 10_000, killerCharacterId, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick); // drains the death: resolves + spawns the boss drop

        Assert.Equal(1, zone.GroundItemCount);

        var outcome = zone.TryClaimGroundItem(1, 1u, "Looter", null, 50, 0, 50, out var item);
        Assert.Equal(GroundItemClaimOutcome.Success, outcome);
        Assert.Equal(SantaGiftItemId, item!.ItemId);
    }

    /// <summary>Always draws the maximum value, keeping the generic item-864 roll (threshold 1000/1,000,000) unreachable.</summary>
    private sealed class MaxValueRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return Math.Max(minValue, maxValue - 1);
        }
    }
}
