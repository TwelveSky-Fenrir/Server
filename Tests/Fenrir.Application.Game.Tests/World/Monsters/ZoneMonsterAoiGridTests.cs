using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers <see cref="Zone.SendExistingMonstersTo" />'s switch from a brute-force scan of every
///     <see cref="Zone.MonstersSnapshot" /> entry to a query against the monster-side AOI grid
///     (<see cref="Zone.SpawnMonster" />/<see cref="Zone.SyncMonsterCell" />/<see cref="Zone.RemoveMonsterFromGrid" />),
///     plus <see cref="Zone.SpawnMonster" />'s own <see cref="AoiGrid.HasAnyNeighbor" /> broadcast pre-check
///     (<c>Zone.Monsters.cs</c>'s <c>BroadcastMonsterAction</c>). Uses a 100-unit AOI cell so cell-boundary
///     arithmetic in the fixtures below is exact and easy to reason about.
/// </summary>
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

    [Fact]
    public void SendExistingMonstersTo_SendsAMonsterWithinTheEnteringPlayersAoiNeighborhood()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(Monster(1, 150f, 50f)); // cell (1, 0) -- one cell over from the arriving player

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f))); // cell (0, 0)
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(FrameWriter.FrameSizeOf<MonsterReplicationResponse>(), ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void SendExistingMonstersTo_DoesNotSendAMonsterOutsideTheEnteringPlayersAoiNeighborhood()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(Monster(1, 350f, 50f)); // cell (3, 0) -- 3 cells away, outside the 3x3 neighborhood

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f))); // cell (0, 0)
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
        var monster = Monster(1, 900f, 50f); // cell (9, 0) -- far outside the player's future neighborhood
        zone.SpawnMonster(monster);

        // Relocates the monster the exact same way MonsterAiSystem.Update does after every position-mutating
        // AI pass: mutate PosX/PosZ directly, then resync the grid.
        monster.PosX = 150f; // cell (1, 0)
        zone.SyncMonsterCell(monster);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f))); // cell (0, 0)
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(FrameWriter.FrameSizeOf<MonsterReplicationResponse>(), ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void SyncMonsterCell_LeavingTheOldNeighborhood_StopsItFromBeingSeenThere()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        var monster = Monster(1, 150f, 50f); // cell (1, 0) -- starts near the player's future entry point
        zone.SpawnMonster(monster);

        monster.PosX = 900f; // cell (9, 0) -- walks far away
        zone.SyncMonsterCell(monster);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f))); // cell (0, 0)
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

        monster.PosX += 1f; // still well within the same 100-unit cell
        zone.SyncMonsterCell(monster);

        Assert.Equal(cellAfterSpawn, monster.CurrentCell);
    }

    /// <summary>
    ///     The scenario <see cref="Zone.RemoveMonsterFromGrid" /> exists to prevent: without it, a dead
    ///     monster's grid entry at its OLD position would leak forever, and a later respawn reusing the exact
    ///     same <see cref="MonsterEntity.ServerIndex" /> (which <c>MonsterSpawnScheduler</c>'s slot-based
    ///     numbering does on every ordinary respawn) at a DIFFERENT position would then be spuriously
    ///     "visible" from that stale old cell too -- because <see cref="Zone.SendExistingMonstersTo" />'s
    ///     dictionary lookup would still resolve that server index to the live (but far-away) respawned
    ///     monster.
    /// </summary>
    [Fact]
    public void MonsterDeath_RemovesTheStaleGridEntry_SoARespawnAtADifferentCellIsNotAlsoVisibleFromTheOldOne()
    {
        var scheduler = new MonsterSpawnScheduler(ZoneTestKit.EmptyWorldData());
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions(), simulationSystems: [scheduler]);

        const int serverIndex = 1;
        var dying = Monster(serverIndex, 50f, 50f); // cell (0, 0)
        zone.SpawnMonster(dying);

        Assert.True(zone.TryDamageMonster(serverIndex, dying.Life, null, out var died, out _));
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick); // drains the death -- must also clear the (0,0) grid entry

        // Same ServerIndex, brand-new instance, spawned far away from the dead one's old (0,0) cell.
        var respawned = Monster(serverIndex, 900f, 50f); // cell (9, 0)
        zone.SpawnMonster(respawned);

        // Enters exactly where the dead monster used to be.
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void DespawnMonsterSilently_RemovesTheGridEntry_SoALaterEnteringPlayerNeverSeesIt()
    {
        var zone = ZoneTestKit.CreateZone(1, SmallCellOptions());
        zone.SpawnMonster(Monster(1, 50f, 50f)); // cell (0, 0)

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

        zone.SpawnMonster(Monster(1, 60f, 60f)); // same cell as the player

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
