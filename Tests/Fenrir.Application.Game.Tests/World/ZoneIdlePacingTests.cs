using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.Tick" />'s population/legacy-tick gating of the periodic monster/ground-item
///     keep-alive rebroadcast: skipping the broadcast for a zone with nobody in it must never affect durable
///     state (ground-item expiry, monster life) and must never delay what a brand-new arrival sees.
/// </summary>
public class ZoneIdlePacingTests
{
    private static MonsterEntity CreateMonster(int serverIndex, float posX, float posZ)
    {
        return MonsterEntity.Create(serverIndex, 1u, WorldDataTestRows.Monster(9001) with { Life = 100 },
            serverIndex, posX, 0, posZ, 300f);
    }

    [Fact]
    public void IdleZone_GroundItemExpirySweepStillRuns_DespiteTheRebroadcastSkip()
    {
        var zone = ZoneTestKit.CreateZone(1);
        zone.SpawnGroundItem(1, 1, 10f, 0f, 10f, "Nobody", "", 0);

        // No player ever entered -- the whole run happens with an empty _players map.
        for (var i = 0; i < 130; i++) // 130 * 500ms = 65s, past the 60s ground-item lifetime
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public void IdleZone_MonsterStateIsUnaffectedByTheRebroadcastSkip()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var monster = CreateMonster(1, 10f, 10f);
        zone.SpawnMonster(monster);

        for (var i = 0; i < 20; i++) // 10s of idle ticks, well past the 5s rebroadcast cadence
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var stillThere));
        Assert.Equal(monster.Life, stillThere!.Life);
    }

    [Fact]
    public void NewArrivalToAPreviouslyIdleZone_ImmediatelySeesExistingMonsterAndGroundItem_OnItsFirstTick()
    {
        // Differential: an otherwise-identical zone/entry with no monster/ground item is the control -- any
        // extra bytes the populated zone sends on the very same first tick must be the monster/ground-item
        // keep-alive, proving it was NOT deferred an extra cadence just because the zone was idle before.
        var populatedZone = ZoneTestKit.CreateZone(1);
        populatedZone.SpawnMonster(CreateMonster(1, 20f, 20f));
        populatedZone.SpawnGroundItem(1, 1, 20f, 0f, 20f, "Nobody", "", 0);

        var emptyZone = ZoneTestKit.CreateZone(1);

        // Idle for far longer than either keep-alive cadence -- both entities' own LastRebroadcastAt
        // timestamps are now stale relative to the zone's simulated clock.
        for (var i = 0; i < 20; i++) // 10s
        {
            populatedZone.Tick(SimulationClock.LegacyTick);
            emptyZone.Tick(SimulationClock.LegacyTick);
        }

        var (populatedSession, populatedPipe) = ZoneTestKit.CreateSession(1);
        var (emptySession, emptyPipe) = ZoneTestKit.CreateSession(2);
        // Same AOI cell (cell size 75) as the monster/ground item above.
        populatedZone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(populatedSession, 1, posX: 20f, posZ: 20f)));
        emptyZone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(emptySession, 1, posX: 20f, posZ: 20f)));

        populatedZone.Tick(SimulationClock.LegacyTick); // drains Enter (hasPlayers flips true) + a full legacy tick
        emptyZone.Tick(SimulationClock.LegacyTick);

        var populatedOutbound = ZoneTestKit.DrainOutbound(populatedPipe).Length;
        var emptyOutbound = ZoneTestKit.DrainOutbound(emptyPipe).Length;

        Assert.True(populatedOutbound > emptyOutbound,
            "a brand-new arrival must see the pre-existing monster/ground item on its first tick, not one cadence later");
    }
}
