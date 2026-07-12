using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class ZoneMonsterAoiGridTests
{
    private static GameServerOptions SmallCellOptions()
    {
        return new GameServerOptions { AoiCellSize = 100f };
    }

    private static MonsterEntity Monster(int serverIndex, float posX, float posZ)
    {
        return MonsterEntity.Create(serverIndex, unchecked((uint)serverIndex), WorldDataTestRows.Monster(700),
            serverIndex, posX, 0f, posZ, 50f);
    }

    private static MonsterEntity WideRadiusMonster(int serverIndex, float posX, float posZ)
    {
        var template = WorldDataTestRows.Monster(700) with { SpecialType = 21 };
        return MonsterEntity.Create(serverIndex, unchecked((uint)serverIndex), template, serverIndex, posX, 0f, posZ,
            50f);
    }

    [Fact]
    public void SendExistingMonstersTo_SendsAMonsterWithinTheEnteringPlayersAoiNeighborhood()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(Monster(1, 150f, 50f));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(FrameWriter.FrameSizeOf<MonsterReplicationResponse>(), ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void SendExistingMonstersTo_DoesNotSendAMonsterOutsideTheEnteringPlayersAoiNeighborhood()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(Monster(1, 350f, 50f));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void SendExistingMonstersTo_SendsAWideRadiusMonster_BeyondTheScale1RadiusButWithinItsOwnScale3Radius()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(WideRadiusMonster(1, 350f, 50f));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(FrameWriter.FrameSizeOf<MonsterReplicationResponse>(), ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void SendExistingMonstersTo_DoesNotSendAWideRadiusMonster_BeyondItsOwnScale3Radius()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(WideRadiusMonster(1, 450f, 50f));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void SendExistingMonstersTo_WithNoMonstersInTheZone_IsANoOp_NoException()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void SyncMonsterCell_MovesTheGridEntry_SoALaterEnteringPlayerSeesTheRelocatedMonster()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        var monster = Monster(1, 900f, 50f);
        zone.SpawnMonster(monster);

        monster.PosX = 150f;
        zone.SyncMonsterCell(monster);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(FrameWriter.FrameSizeOf<MonsterReplicationResponse>(), ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void SyncMonsterCell_LeavingTheOldNeighborhood_StopsItFromBeingSeenThere()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        var monster = Monster(1, 150f, 50f);
        zone.SpawnMonster(monster);

        monster.PosX = 900f;
        zone.SyncMonsterCell(monster);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void SyncMonsterCell_IsANoOp_WhenThePassLeftTheMonsterInTheSameCell()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        var monster = Monster(1, 50f, 50f);
        zone.SpawnMonster(monster);
        var cellAfterSpawn = monster.CurrentCell;

        monster.PosX += 1f;
        zone.SyncMonsterCell(monster);

        Assert.Equal(cellAfterSpawn, monster.CurrentCell);
    }

    [Fact]
    public void MonsterDeath_RemovesTheStaleGridEntry_SoARespawnAtADifferentCellIsNotAlsoVisibleFromTheOldOne()
    {
        var scheduler = new MonsterSpawnScheduler(ZoneTestKit.EmptyWorldData());
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions(), simulationSystems: [scheduler]);

        const int serverIndex = 1;
        var dying = Monster(serverIndex, 50f, 50f);
        zone.SpawnMonster(dying);

        Assert.True(zone.TryDamageMonster(serverIndex, dying.Life, null, out var died, out _));
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick);

        var respawned = Monster(serverIndex, 900f, 50f);
        zone.SpawnMonster(respawned);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void DespawnMonsterSilently_RemovesTheGridEntry_SoALaterEnteringPlayerNeverSeesIt()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(Monster(1, 50f, 50f));

        zone.DespawnMonsterSilently(1);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void SpawnMonster_BroadcastsTheCreationFrame_ToANearbyPlayer()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        zone.SpawnMonster(Monster(1, 60f, 60f));

        Assert.Equal(FrameWriter.FrameSizeOf<MonsterReplicationResponse>(), ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void SpawnMonster_WithNoNeighborsAnywhere_SendsNothing_AndDoesNotThrow()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());

        zone.SpawnMonster(Monster(1, 0f, 0f));

        Assert.Equal(1, zone.MonsterCount);
    }
}
