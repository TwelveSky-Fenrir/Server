using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterRebroadcastStaggerTests
{
    private static readonly int FrameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();

    private static readonly int LegacyTicksPerMonsterInterval =
        (int)(SimulationClock.MonsterRebroadcastInterval.Ticks / SimulationClock.LegacyTick.Ticks);

    private static MonsterEntity Monster(int serverIndex, float posX, float posZ)
    {
        return MonsterEntity.Create(serverIndex, unchecked((uint)serverIndex), WorldDataTestRows.Monster(700),
            serverIndex, posX, 0f, posZ);
    }

    [Fact]
    public void ManyMonstersSpawnedAtTheSameInstant_GetDifferentLastRebroadcastAt_DueToStaggering()
    {
        var zone = ZoneTestKit.CreateZone(1);
        for (var i = 1; i <= 5; i++)
            zone.SpawnMonster(Monster(i, 50f, 50f));

        Assert.True(zone.TryGetMonster(1, out var m1));
        Assert.True(zone.TryGetMonster(3, out var m3));
        Assert.True(zone.TryGetMonster(5, out var m5));

        Assert.NotEqual(m1!.LastRebroadcastAt, m3!.LastRebroadcastAt);
        Assert.NotEqual(m3.LastRebroadcastAt, m5!.LastRebroadcastAt);
        Assert.NotEqual(m1.LastRebroadcastAt, m5.LastRebroadcastAt);
    }

    [Fact]
    public void ManyMonstersSpawnedAtTheSameInstant_RebroadcastsSpreadAcrossTheWindow_NeverAllInOneTick()
    {
        var zone = ZoneTestKit.CreateZone(1);
        const int monsterCount = 20;

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));

        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        for (var i = 1; i <= monsterCount; i++)
            zone.SpawnMonster(Monster(i, 50f, 50f));

        ZoneTestKit.DrainOutbound(pipe);

        var framesPerTick = new List<int>();
        for (var tick = 0; tick < LegacyTicksPerMonsterInterval; tick++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            var bytes = ZoneTestKit.DrainOutbound(pipe);
            Assert.Equal(0, bytes.Length % FrameSize);
            framesPerTick.Add(bytes.Length / FrameSize);
        }

        Assert.Equal(monsterCount, framesPerTick.Sum());

        Assert.All(framesPerTick, perTickCount => Assert.True(perTickCount < monsterCount,
            $"one tick carried {perTickCount}/{monsterCount} monster keep-alives -- the thundering herd was not fixed"));

        var ticksWithTraffic = framesPerTick.Count(c => c > 0);
        Assert.True(ticksWithTraffic > 1,
            $"expected keep-alives spread across multiple ticks, got traffic in only {ticksWithTraffic} of {LegacyTicksPerMonsterInterval}");

        Assert.Equal(Enumerable.Repeat(2, LegacyTicksPerMonsterInterval), framesPerTick);
    }

    [Fact]
    public void SingleMonster_StillRebroadcastsWithinTheFullInterval_StaggerNeverDelaysItPastTheOldWorstCase()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        zone.SpawnMonster(Monster(1, 50f, 50f));
        ZoneTestKit.DrainOutbound(pipe);

        var totalFrames = 0;
        for (var tick = 0; tick < LegacyTicksPerMonsterInterval; tick++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            totalFrames += ZoneTestKit.DrainOutbound(pipe).Length / FrameSize;
        }

        Assert.Equal(1, totalFrames);
    }
}
