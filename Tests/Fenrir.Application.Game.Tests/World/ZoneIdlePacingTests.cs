using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

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

        for (var i = 0; i < 130; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public void IdleZone_MonsterStateIsUnaffectedByTheRebroadcastSkip()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var monster = CreateMonster(1, 10f, 10f);
        zone.SpawnMonster(monster);

        for (var i = 0; i < 20; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var stillThere));
        Assert.Equal(monster.Life, stillThere!.Life);
    }

    [Fact]
    public void NewArrivalToAPreviouslyIdleZone_ImmediatelySeesExistingMonsterAndGroundItem_OnItsFirstTick()
    {
        var populatedZone = ZoneTestKit.CreateZone(1);
        populatedZone.SpawnMonster(CreateMonster(1, 20f, 20f));
        populatedZone.SpawnGroundItem(1, 1, 20f, 0f, 20f, "Nobody", "", 0);

        var emptyZone = ZoneTestKit.CreateZone(1);

        for (var i = 0; i < 20; i++)
        {
            populatedZone.Tick(SimulationClock.LegacyTick);
            emptyZone.Tick(SimulationClock.LegacyTick);
        }

        var (populatedSession, populatedPipe) = ZoneTestKit.CreateSession(1);
        var (emptySession, emptyPipe) = ZoneTestKit.CreateSession(2);
        populatedZone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(populatedSession, 1, posX: 20f, posZ: 20f)));
        emptyZone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(emptySession, 1, posX: 20f, posZ: 20f)));

        populatedZone.Tick(SimulationClock.LegacyTick);
        emptyZone.Tick(SimulationClock.LegacyTick);

        var populatedOutbound = ZoneTestKit.DrainOutbound(populatedPipe).Length;
        var emptyOutbound = ZoneTestKit.DrainOutbound(emptyPipe).Length;

        Assert.True(populatedOutbound > emptyOutbound,
            "a brand-new arrival must see the pre-existing monster/ground item on its first tick, not one cadence later");
    }
}
