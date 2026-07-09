using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Elevated-tier "moncall" summon-monster GM command (legacy PROCESS_DATA_SEND, opcode 19, tSort 506 --
// there is no dedicated legacy wire opcode for this command; GenericActionHandler decodes
// GmSummonMonsterPayload out of GenericActionRequest.Data before calling into this service --
// Server/ts25zone/S04_MyWork04.cpp:1133-1145). GmSummonMonsterService performs no validation of its own
// (matching legacy's own SummonMonsterForSpecial call, which is also unchecked) and unconditionally reports
// success once the tier gate passes; the actual spawn -- or silent no-op for an unknown template id -- is
// applied by Zone.Monsters.cs's SpawnGmSummonedMonster on the zone's own tick thread via
// Zone.PostTribeProgressCommandAndWaitAsync. Covers: the Elevated-tier privilege gate, a known-template-id
// spawn (MonsterCount increments), an unknown-template-id no-op (MonsterCount unchanged, still an accepted
// ack -- legacy performs no id validation either), and the game.EventLog (Category=GmAction) audit row
// written on every accepted invocation regardless of whether the spawn actually happened.
public class GmSummonMonsterServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;
    private const int Sort = 506;
    private const int GenericActionDataLength = 130;
    private const int KnownMonsterId = 900;
    private const int UnknownMonsterId = 901; // absent from the test catalog.

    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GmSummonMonsterService.HandleAsync never completed.");
        }

        await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeEventLogRepository EventLog) SetUp(short accountGrade)
    {
        var monstersById = new Dictionary<int, MonsterDefinition>
        {
            [KnownMonsterId] = new(WorldDataTestRows.Monster(KnownMonsterId), null, [], [], [], null)
        }.ToFrozenDictionary();

        var zone = ZoneTestKit.CreateZone(MapId, worldData: ZoneTestKit.EmptyWorldData(monstersById: monstersById));
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!, new FakeEventLogRepository());
    }

    private static byte[] RequestData(int monsterId)
    {
        var data = new byte[GenericActionDataLength];
        new GmSummonMonsterPayload { Value = monsterId }.Write(data);
        return data;
    }

    /// <summary>
    ///     Tolerates the BroadcastMonsterAction creation announcement Zone.SpawnMonster sends to every AOI
    ///     neighbor -- always including the summoning GM's own client here, since a "moncall" monster spawns
    ///     exactly at the caller's own position -- landing on this same pipe BEFORE this service's own ack,
    ///     same "tail of whatever is currently buffered" posture GmCreateItemServiceTests documents for its
    ///     own ground-item AOI creation broadcast.
    /// </summary>
    private static async Task AssertResponseSentAsync(FakeDuplexPipe pipe, GenericActionResponse expected)
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var frame = new byte[FrameWriter.FrameSizeOf<GenericActionResponse>()];
        FrameWriter.WriteFrame(in expected, frame);
        Assert.True(actual.Length >= frame.Length,
            $"Expected at least {frame.Length} bytes on the wire, got {actual.Length}.");
        Assert.Equal(frame, actual[^frame.Length..]);
    }

    [Fact]
    public async Task HandleAsync_CallerNotElevatedTier_AbortsWithNoReply_LogsNothing_AndSpawnsNoMonster()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Basic);
        var service = new GmSummonMonsterService(eventLog, NullLogger<GmSummonMonsterService>.Instance);
        var data = RequestData(KnownMonsterId);

        await RunToCompletionAsync(
            service.HandleAsync(new GmSummonMonsterPayload { Value = KnownMonsterId }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_KnownTemplateId_SpawnsOneMonster_AcksAccepted_AndLogsAuditRow()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmSummonMonsterService(eventLog, NullLogger<GmSummonMonsterService>.Instance);
        var data = RequestData(KnownMonsterId);

        await RunToCompletionAsync(
            service.HandleAsync(new GmSummonMonsterPayload { Value = KnownMonsterId }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, zone.MonsterCount);
        await AssertResponseSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)6, logged.EventCode); // GmActionEventCodes.SummonMonster (internal, not visible here).
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal($"MonsterId={KnownMonsterId};MapId={MapId}", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_UnknownTemplateId_SpawnsNothing_ButStillAcksAcceptedAndLogsSuccess()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmSummonMonsterService(eventLog, NullLogger<GmSummonMonsterService>.Instance);
        var data = RequestData(UnknownMonsterId);

        await RunToCompletionAsync(
            service.HandleAsync(new GmSummonMonsterPayload { Value = UnknownMonsterId }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, zone.MonsterCount);
        // No spawn -> no BroadcastMonsterAction noise -- the ack is the only frame on the wire this time.
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome); // Unconditional success -- legacy validates nothing either.
        Assert.Equal($"MonsterId={UnknownMonsterId};MapId={MapId}", logged.Payload);
    }
}
