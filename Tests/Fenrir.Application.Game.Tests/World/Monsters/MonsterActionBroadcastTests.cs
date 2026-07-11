using System.Buffers.Binary;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterActionBroadcastTests
{
    private static WorldDataCache AggressiveMonsterCache()
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = 2,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 1000,
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
        {
            Number = 1, LocationX = 0, LocationY = 0, LocationZ = 0, Radius = 50
        };
        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static Zone CreateAggressiveZone(WorldDataCache cache)
    {
        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        return ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);
    }

    [Fact]
    public void MonsterAcquiringTarget_ImmediatelyBroadcastsChangeFrame_WithCorrectTargetDescriptor()
    {
        var zone = CreateAggressiveZone(AggressiveMonsterCache());
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        (int ServerIndex, ObjectForMonster Data, int CheckChangeActionState)? acquisition = null;
        for (var i = 0; i < 10 && acquisition is null; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            foreach (var frame in ParseMonsterFrames(ZoneTestKit.DrainOutbound(pipe)))
                if (frame.Data.Action.Sort == (int)MonsterAiState.Chase && frame.CheckChangeActionState == 1)
                    acquisition = frame;
        }

        Assert.True(acquisition is not null,
            "monster never emitted an immediate op18 Chase change-broadcast (checkChangeActionState=1) on aggro");

        var action = acquisition!.Value.Data.Action;
        Assert.Equal(10, action.TargetObjectIndex);
        Assert.Equal(10, action.TargetObjectUniqueNumber);
        Assert.Equal(0, action.TargetObjectSort);
        Assert.Equal(10f, action.TargetLocation[0]);
    }

    [Fact]
    public void IdleMonster_OnEnterCatchUpFrame_UsesMinusOneTargetIndex_AndKeepAliveState()
    {
        var zone = ZoneTestKit.CreateZone(1, new GameServerOptions { AoiCellSize = 100_000f });
        zone.SpawnMonster(MonsterEntity.Create(1, 1u, WorldDataTestRows.Monster(700), 1, 0f, 0f, 0f, 50f));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 5, posZ: 0)));
        zone.Tick(SimulationClock.LegacyTick);

        var frames = ParseMonsterFrames(ZoneTestKit.DrainOutbound(pipe));
        Assert.NotEmpty(frames);
        foreach (var frame in frames)
        {
            Assert.Equal(-1, frame.Data.Action.TargetObjectIndex);
            Assert.Equal(0, frame.Data.Action.TargetObjectUniqueNumber);
            Assert.Equal(2, frame.CheckChangeActionState);
        }
    }

        private static List<(int ServerIndex, ObjectForMonster Data, int CheckChangeActionState)> ParseMonsterFrames(
        byte[] bytes)
    {
        var frames = new List<(int, ObjectForMonster, int)>();
        var frameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        Assert.Equal(0, bytes.Length % frameSize);

        for (var offset = 0; offset + frameSize <= bytes.Length; offset += frameSize)
        {
            Assert.Equal(MonsterReplicationResponse.Opcode, bytes[offset]);
            var payload = bytes.AsSpan(offset + 1);
            var serverIndex = BinaryPrimitives.ReadInt32LittleEndian(payload);
            Assert.True(ObjectForMonster.TryRead(payload.Slice(8, ObjectForMonster.WireSize), out var data));
            var checkChange = BinaryPrimitives.ReadInt32LittleEndian(payload[(8 + ObjectForMonster.WireSize)..]);
            frames.Add((serverIndex, data, checkChange));
        }

        return frames;
    }

    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}
