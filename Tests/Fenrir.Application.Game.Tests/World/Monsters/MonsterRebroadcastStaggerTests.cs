using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Reproduces and fixes a confirmed, directly-observed thundering-herd bug: <c>MonsterSpawnScheduler</c>'s
///     <c>InitialPopDone</c> branch spawns EVERY configured spawn-region slot unconditionally on a zone's first
///     <c>Simulate</c> call, all within the same <see cref="Zone.Tick" />, all reading the exact same zone
///     clock value. Before the fix, <see cref="Zone.SpawnMonster" /> stamped every one of those monsters'
///     <see cref="MonsterEntity.LastRebroadcastAt" /> with that identical value, so the whole population became
///     "due" for <c>Zone.RebroadcastMonsters</c>' 5 s keep-alive on the exact same later tick -- a burst of one
///     individual <c>MonsterReplicationResponse</c> send per monster per nearby recipient, then perfect
///     re-synchronization (that same tick re-stamps all of them to the SAME new clock value), repeating every
///     5 s for the zone's entire lifetime. Directly evidenced in a real GameServer's own debug log as dozens of
///     consecutive "packet sent, opcode 18" lines immediately followed by "Zone 6 tick took 684.0 ms (budget
///     50.0 ms)"-style overruns, recurring periodically.
/// </summary>
public class MonsterRebroadcastStaggerTests
{
    private static readonly int FrameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();

    private static readonly int LegacyTicksPerMonsterInterval =
        (int)(SimulationClock.MonsterRebroadcastInterval.Ticks / SimulationClock.LegacyTick.Ticks);

    private static MonsterEntity Monster(int serverIndex, float posX, float posZ)
    {
        return MonsterEntity.Create(serverIndex, unchecked((uint)serverIndex), WorldDataTestRows.Monster(700),
            serverIndex, posX, 0f, posZ, 50f);
    }

    [Fact]
    public void ManyMonstersSpawnedAtTheSameInstant_GetDifferentLastRebroadcastAt_DueToStaggering()
    {
        // Sanity check on the confirmed root cause itself, independent of the burst-level test below:
        // MonsterSpawnScheduler really does hand every slot to Zone.SpawnMonster at the exact same zone-clock
        // value (its own InitialPopDone loop has no per-slot delay/tick in between).
        var zone = ZoneTestKit.CreateZone(1);
        for (var i = 1; i <= 5; i++)
            zone.SpawnMonster(Monster(i, 50f, 50f));

        Assert.True(zone.TryGetMonster(1, out var m1));
        Assert.True(zone.TryGetMonster(3, out var m3));
        Assert.True(zone.TryGetMonster(5, out var m5));

        // All spawned before any Tick call -- the zone clock was 0 for every one of them. If LastRebroadcastAt
        // were seeded as a plain "= _clock" (the pre-fix behavior), m1/m3/m5 would be byte-identical too. The
        // fix makes them differ (each staggered by its own ServerIndex) despite sharing the same spawn clock.
        Assert.NotEqual(m1!.LastRebroadcastAt, m3!.LastRebroadcastAt);
        Assert.NotEqual(m3.LastRebroadcastAt, m5!.LastRebroadcastAt);
        Assert.NotEqual(m1.LastRebroadcastAt, m5.LastRebroadcastAt);
    }

    [Fact]
    public void ManyMonstersSpawnedAtTheSameInstant_RebroadcastsSpreadAcrossTheWindow_NeverAllInOneTick()
    {
        var zone = ZoneTestKit.CreateZone(1); // default AoiCellSize (1000) keeps everything below in one cell
        const int monsterCount = 20;

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));

        // Resolve the Enter command (and its own one-shot mutual-visibility exchange) on its own tick, BEFORE
        // any monster exists, so the population spawned next is cleanly isolated from that unrelated traffic.
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        // Every monster spawned back-to-back with no tick in between -- the exact "all popped in the same
        // Zone.Tick, same zone-clock value" shape MonsterSpawnScheduler.Simulate's InitialPopDone branch
        // produces for a zone's whole spawn-region pool on boot.
        for (var i = 1; i <= monsterCount; i++)
            zone.SpawnMonster(Monster(i, 50f, 50f));

        // Discard the (unrelated, one-shot, correctness-required) per-monster creation announcements
        // (BroadcastMonsterAction's own action=1 call from inside SpawnMonster) -- only the RECURRING 5 s
        // keep-alive cadence is under test here.
        ZoneTestKit.DrainOutbound(pipe);

        var framesPerTick = new List<int>();
        for (var tick = 0; tick < LegacyTicksPerMonsterInterval; tick++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            var bytes = ZoneTestKit.DrainOutbound(pipe);
            Assert.Equal(0, bytes.Length % FrameSize); // only whole MonsterReplicationResponse frames, nothing else
            framesPerTick.Add(bytes.Length / FrameSize);
        }

        // Correctness preserved: every monster still gets exactly one keep-alive rebroadcast somewhere across
        // the whole window -- staggering must never drop or duplicate one.
        Assert.Equal(monsterCount, framesPerTick.Sum());

        // The bug, precisely: pre-fix, EVERY monster shared the identical LastRebroadcastAt, so this same loop
        // would show the whole population (20 frames) landing in a single tick and 0 in every other one. The
        // fix must spread them out -- no single tick may carry the whole population...
        Assert.All(framesPerTick, perTickCount => Assert.True(perTickCount < monsterCount,
            $"one tick carried {perTickCount}/{monsterCount} monster keep-alives -- the thundering herd was not fixed"));

        // ...and, positively, the rebroadcasts must actually be spread across MULTIPLE distinct ticks, not just
        // "one fewer than before".
        var ticksWithTraffic = framesPerTick.Count(c => c > 0);
        Assert.True(ticksWithTraffic > 1,
            $"expected keep-alives spread across multiple ticks, got traffic in only {ticksWithTraffic} of {LegacyTicksPerMonsterInterval}");

        // Exact, deterministic distribution: 20 monsters (ServerIndex 1..20) bucketed into 10 legacy-tick-wide
        // slots across the 5 s window (ServerIndex and ServerIndex+10 alias to the same bucket) land exactly 2
        // per tick, one bucket newly coming due each successive tick -- pinning this is a strong regression
        // guard for both the stagger formula and Zone.Tick's own once-per-legacy-tick rebroadcast gating.
        Assert.Equal(Enumerable.Repeat(2, LegacyTicksPerMonsterInterval), framesPerTick);
    }

    [Fact]
    public void SingleMonster_StillRebroadcastsWithinTheFullInterval_StaggerNeverDelaysItPastTheOldWorstCase()
    {
        // The contract this fix must never break: "every monster gets rebroadcast at least once every 5 s."
        // Staggering only ever pulls an entity's first due time EARLIER within the window, never later, so a
        // lone monster (however unlucky its ServerIndex bucket) must still fire within one full interval.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        zone.SpawnMonster(Monster(1, 50f, 50f));
        ZoneTestKit.DrainOutbound(pipe); // discard the creation announcement

        var totalFrames = 0;
        for (var tick = 0; tick < LegacyTicksPerMonsterInterval; tick++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            totalFrames += ZoneTestKit.DrainOutbound(pipe).Length / FrameSize;
        }

        Assert.Equal(1, totalFrames);
    }
}
