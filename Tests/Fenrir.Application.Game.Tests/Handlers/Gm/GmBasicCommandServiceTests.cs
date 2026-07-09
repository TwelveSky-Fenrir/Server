using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Basic-tier (GmCommandTier.Basic, legacy uUserSort >= 1) GM command family -- legacy PROCESS_DATA_SEND,
// opcode 19, tSort 501/502/507/508/510/511/512/513/514/515/516/517/518/520/521/522
// (Server/ts25zone/S04_MyWork04.cpp:290-339,925-1622,2105-2123). There is no dedicated legacy wire opcode
// for any of these; GenericActionHandler decodes each embedded payload out of GenericActionRequest.Data
// before calling into IGmBasicCommandService -- see that interface's own class remarks for the full shared
// framing contract every group below exercises (default-failure-until-proven-otherwise, tier-gate-failure
// -> Faulted disconnect with no reply). One test class per sub-command (or tightly-coupled HIDE/SHOW,
// EQUIP/UNEQUIP, NCHAT/YCHAT pair sharing one wire shape/handler method), mirroring
// GmBlockAvatarServiceTests.cs's own per-command-family layout for the sibling tSort 519.
internal static class GmBasicTestSupport
{
    public const int AccountId = 1;
    public const short MapId = 1;
    public const int GenericActionDataLength = 130;
    public const int GmDataSize = 100; // MAX_TRIBE_WORK_SIZE, matches GmCommandResponse.GmData

    /// <summary>
    ///     Drives a <c>Post*CommandAndWaitAsync</c>-backed call to completion by repeatedly draining the zone's
    ///     command inbox. Ticks with <see cref="TimeSpan.Zero" /> elapsed simulated time rather than a fixed
    ///     step: the tribe-progress inbox drain (what actually resolves the awaited
    ///     <c>TaskCompletionSource</c>) runs unconditionally on every <see cref="Zone.Tick" /> call regardless
    ///     of elapsed time, but the periodic keep-alive rebroadcasts (avatars every 3.5s -- <see cref="Zone" />'s
    ///     own remarks) only fire once real *simulated* time crosses their threshold. Because the
    ///     <c>TaskCompletionSource</c> backing <see cref="Zone.PostTribeProgressCommandAndWaitAsync" /> uses
    ///     <c>RunContinuationsAsynchronously</c>, how many loop iterations this needs before <paramref name="pending" />
    ///     actually observes completion depends on ThreadPool scheduling fairness under test-suite load, not on
    ///     this method -- an unbounded elapsed-time step would risk crossing that 3.5s threshold under
    ///     contention and flooding a second in-zone character's pipe with spurious keep-alive noise this test
    ///     harness never asked for. Zero elapsed time keeps that threshold permanently unreached no matter how
    ///     many iterations this loop needs.
    /// </summary>
    public static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.Zero);
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GmBasicCommandService call never completed.");
        }

        await task;
    }

    public static (ZoneRegistry Registry, Zone Zone) CreateWorld(WorldDataCache? worldData = null)
    {
        var registry = ZoneTestKit.CreateRegistry(worldData: worldData);
        registry.Initialize([MapId]);
        return (registry, registry[MapId]);
    }

    public static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Enter(Zone zone,
        int characterId, string name, short accountGrade = 0, int accountId = AccountId, byte tribe = 1)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(accountId, characterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, name, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    /// <summary>130-byte GenericActionRequest.Data region, optionally pre-filled by an embedded payload's own Write.</summary>
    public static byte[] RequestData(Action<byte[]>? write = null)
    {
        var data = new byte[GenericActionDataLength];
        write?.Invoke(data);
        return data;
    }

    public static byte[] PackedCoordinateGmData(float x, float y, float z)
    {
        var gmData = new byte[GmDataSize];
        new GmMoveCoordinatePayload { Location = [x, y, z] }.Write(gmData);
        return gmData;
    }

    public static byte[] MapIdGmData(short mapId)
    {
        var gmData = new byte[GmDataSize];
        BinaryPrimitives.WriteInt32LittleEndian(gmData, mapId);
        return gmData;
    }

    public static byte[] ZeroGmData()
    {
        return new byte[GmDataSize];
    }

    /// <summary>
    ///     Reads exactly one pending pipe read and asserts its tail matches <paramref name="expected" />'s own
    ///     frame -- tolerates a preceding AOI/neighbor-broadcast frame landing on the same pipe first, same
    ///     "tail of whatever is currently buffered" posture GmExpGrantServiceTests/GmSummonMonsterServiceTests
    ///     document for their own ambient-broadcast quirks.
    /// </summary>
    public static async Task AssertTailFrameAsync<TPacket>(FakeDuplexPipe pipe, TPacket expected)
        where TPacket : struct, IOutgoingPacket
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var frame = new byte[FrameWriter.FrameSizeOf<TPacket>()];
        FrameWriter.WriteFrame(in expected, frame);
        Assert.True(actual.Length >= frame.Length,
            $"Expected at least {frame.Length} bytes on the wire, got {actual.Length}.");
        Assert.Equal(frame, actual[^frame.Length..]);
    }

    /// <summary>Asserts a pipe carries exactly these two frames, back to back, and nothing else.</summary>
    public static async Task AssertExactSequenceAsync<T1, T2>(FakeDuplexPipe pipe, T1 first, T2 second)
        where T1 : struct, IOutgoingPacket
        where T2 : struct, IOutgoingPacket
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var frame1 = new byte[FrameWriter.FrameSizeOf<T1>()];
        FrameWriter.WriteFrame(in first, frame1);
        var frame2 = new byte[FrameWriter.FrameSizeOf<T2>()];
        FrameWriter.WriteFrame(in second, frame2);
        Assert.Equal([.. frame1, .. frame2], actual);
    }
}

// HIDE(501)/SHOW(502) -- self-only visibility toggle (S04_MyWork04.cpp:933-958). Sends the dedicated
// AvatarStatUpdateResponse (self-only, Sort=9) immediately followed by the shared generic acknowledgment.
public class GmBasicVisibilityServiceTests
{
    private const int CharacterId = 10;
    private const int HideSort = 501;
    private const int ShowSort = 502;
    private const int VisibilityStatSort = 9;

    [Fact]
    public async Task HandleVisibilityAsync_CallerNotBasicTier_AbortsWithNoReply_AndLeavesVisibleStateUnchanged()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "NotAGm");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleVisibilityAsync(HideSort, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal(1, state.VisibleState); // world-entry default, never touched
    }

    [Fact]
    public async Task HandleVisibilityAsync_Hide_SetsVisibleStateToZero_SendsStatUpdateThenAck()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleVisibilityAsync(HideSort, data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, state.VisibleState);
        await GmBasicTestSupport.AssertExactSequenceAsync(pipe,
            new AvatarStatUpdateResponse { Sort = VisibilityStatSort, Value = 0, Value2 = 0 },
            new GenericActionResponse { Result = 0, Sort = HideSort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleVisibilityAsync_Show_SetsVisibleStateToOne_SendsStatUpdateThenAck()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        state.VisibleState = 0;
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleVisibilityAsync(ShowSort, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(1, state.VisibleState);
        await GmBasicTestSupport.AssertExactSequenceAsync(pipe,
            new AvatarStatUpdateResponse { Sort = VisibilityStatSort, Value = 1, Value2 = 0 },
            new GenericActionResponse { Result = 0, Sort = ShowSort, Data = data, RuneValue = 0 });
    }
}

// Self-teleport-to-coordinate MOVE (507) -- unconditional absolute position overwrite, no bounds/collision
// check (S04_MyWork04.cpp:1146-1164). Shared generic acknowledgment only, no dedicated response.
public class GmBasicSelfTeleportServiceTests
{
    private const int CharacterId = 10;
    private const int Sort = 507;

    [Fact]
    public async Task HandleSelfTeleportAsync_CallerNotBasicTier_AbortsWithNoReply_AndLeavesPositionUnchanged()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "NotAGm");
        var originalX = state.PosX;
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d =>
            new GmMoveCoordinatePayload { Location = [500f, 1f, 500f] }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleSelfTeleportAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal(originalX, state.PosX);
    }

    [Fact]
    public async Task HandleSelfTeleportAsync_BasicTier_OverwritesPositionUnconditionally_AndAcksSuccess()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d =>
            new GmMoveCoordinatePayload { Location = [500f, 1f, 700f] }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleSelfTeleportAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(500f, state.PosX);
        Assert.Equal(1f, state.PosY);
        Assert.Equal(700f, state.PosZ);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

// DIE (508) -- force-invalidates a live monster instance by raw table index, not a player
// (S04_MyWork04.cpp:1165-1187). Legacy DOES audit-log this one before the mutation.
public class GmBasicForceKillMonsterServiceTests
{
    private const int SessionId = 10;
    private const int Sort = 508;
    private const int MonsterServerIndex = 42;

    private static MonsterEntity SpawnMonster(Zone zone, int serverIndex, int life)
    {
        var template = WorldDataTestRows.Monster(900) with { Life = life };
        var monster = MonsterEntity.Create(serverIndex, 1u, template, serverIndex, 0f, 0f, 0f, 100f);
        zone.SpawnMonster(monster);
        return monster;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe) CreateSession(short accountGrade)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(SessionId);
        session.MarkTicketConsumed(GmBasicTestSupport.AccountId, SessionId, null, accountGrade);
        return (session, pipe);
    }

    [Fact]
    public async Task
        HandleForceKillMonsterAsync_CallerNotBasicTier_AbortsWithNoReply_LogsNothing_AndLeavesMonsterAlive()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        SpawnMonster(zone, MonsterServerIndex, 500);
        var (session, pipe) = CreateSession(0);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d =>
            new GmMonsterInstanceIndexPayload { MonsterIndex = MonsterServerIndex }.Write(d));

        await service.HandleForceKillMonsterAsync(data, session, zone, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.True(zone.TryGetMonster(MonsterServerIndex, out var monster));
        Assert.Equal(500, monster!.Life);
    }

    [Fact]
    public async Task HandleForceKillMonsterAsync_IndexOutOfRange_SilentlyDropped_AcksFailure()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe) = CreateSession(1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d =>
            new GmMonsterInstanceIndexPayload { MonsterIndex = 3000 }.Write(d)); // capacity is [0,3000)

        await service.HandleForceKillMonsterAsync(data, session, zone, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleForceKillMonsterAsync_NoMonsterAtIndex_SilentlyDropped_AcksFailure()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe) = CreateSession(1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d =>
            new GmMonsterInstanceIndexPayload { MonsterIndex = MonsterServerIndex }.Write(d));

        await service.HandleForceKillMonsterAsync(data, session, zone, CancellationToken.None);

        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleForceKillMonsterAsync_LiveInstance_ForceKillsIt_LogsAuditRow_AcksSuccess()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var monster = SpawnMonster(zone, MonsterServerIndex, 500);
        var (session, pipe) = CreateSession(1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d =>
            new GmMonsterInstanceIndexPayload { MonsterIndex = MonsterServerIndex }.Write(d));

        await service.HandleForceKillMonsterAsync(data, session, zone, CancellationToken.None);

        Assert.False(zone.TryGetMonster(MonsterServerIndex, out _));
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)7, logged.EventCode); // GmActionEventCodes.MonsterForceKill (internal, not visible here)
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(GmBasicTestSupport.AccountId, logged.ActorAccountId);
        Assert.Equal(SessionId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(monster.Template.MonsterId, logged.ItemId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal($"ServerIndex={MonsterServerIndex};MonsterName={monster.Template.Name}", logged.Payload);
    }
}

// TRIBE (510) -- self-only tribe change, selector 0-3, no target-character parameter (S04_MyWork04.cpp:
// 1203-1264). A selector equal to the current tribe, or outside 0-3, is silently dropped; a successful
// change forces a logout as the completion signal, sending NO acknowledgment of any kind.
public class GmBasicTribeChangeServiceTests
{
    private const int CharacterId = 10;

    private static (ZoneRegistry Registry, Zone Zone, ZoneClientSession Session, FakeDuplexPipe Pipe,
        PlayerRuntimeState State) SetUp(short accountGrade, byte initialTribe = 1)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", accountGrade,
            tribe: initialTribe);
        return (registry, zone, session, pipe, state);
    }

    [Fact]
    public async Task HandleTribeChangeAsync_CallerNotBasicTier_AbortsWithNoReply_AndLeavesTribeUnchanged()
    {
        var (registry, zone, session, pipe, state) = SetUp(0);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTribeChangePayload { Tribe = 2 }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTribeChangeAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal((byte)1, state.Tribe);
    }

    [Theory]
    [InlineData(1)] // equal to the current tribe
    [InlineData(4)] // out of the live [0,3] range
    [InlineData(-1)] // out of the live [0,3] range
    public async Task HandleTribeChangeAsync_SelectorEqualOrOutOfRange_SilentlyDropped(int selector)
    {
        var (registry, zone, session, pipe, state) = SetUp(1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTribeChangePayload { Tribe = selector }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTribeChangeAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal((byte)1, state.Tribe);
    }

    [Fact]
    public async Task HandleTribeChangeAsync_OrdinarySelector_UpdatesTribeAndPreviousTribe_ForcesLogout_NoReply()
    {
        var (registry, zone, session, pipe, state) = SetUp(1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTribeChangePayload { Tribe = 2 }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTribeChangeAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal((byte)2, state.Tribe);
        Assert.Equal((byte)2, state.PreviousTribe);
        Assert.Equal(DisconnectReason.GmCommandLogout, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleTribeChangeAsync_SpecialValue3_UpdatesTribeOnly_LeavesPreviousTribeUntouched()
    {
        var (registry, zone, session, pipe, state) = SetUp(1);
        Assert.Equal((byte)0, state.PreviousTribe); // world-entry default this test relies on
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTribeChangePayload { Tribe = 3 }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTribeChangeAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal((byte)3, state.Tribe);
        Assert.Equal((byte)0, state.PreviousTribe);
        Assert.Equal(DisconnectReason.GmCommandLogout, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }
}

// EQUIP(511)/UNEQUIP(512) -- self-only "special state" marker write, no item involved
// (S04_MyWork04.cpp:1265-1298). Shared generic acknowledgment only.
public class GmBasicSelfSpecialStateServiceTests
{
    private const int CharacterId = 10;
    private const int EquipSort = 511;
    private const int UnequipSort = 512;

    [Fact]
    public async Task HandleSelfSpecialStateAsync_CallerNotBasicTier_AbortsWithNoReply_AndLeavesSpecialStateUnchanged()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "NotAGm");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleSelfSpecialStateAsync(EquipSort, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal(0, state.SpecialState);
    }

    [Fact]
    public async Task HandleSelfSpecialStateAsync_Equip_SetsSpecialStateToOne_AcksSuccess()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleSelfSpecialStateAsync(EquipSort, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(1, state.SpecialState);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = EquipSort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleSelfSpecialStateAsync_Unequip_SetsSpecialStateToZero_AcksSuccess()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        state.SpecialState = 1;
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleSelfSpecialStateAsync(UnequipSort, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(0, state.SpecialState);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = UnequipSort, Data = data, RuneValue = 0 });
    }
}

// FIND (513) -- process-local by-name lookup, reports a coordinate/zone-number (GmCommandResponse) then
// always acks success, sentinel-encoding "not found" as a zero-filled GmData region instead of a failure
// result (S04_MyWork04.cpp:1299-1323). See IGmBasicCommandService's own remarks for why this simplifies
// legacy's cluster-wide blocking upstream lookup to a process-local one.
public class GmBasicFindServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int Sort = 513;

    // Legacy B_GM_COMMAND_INFO(1, ...) literal tag written into GmCommandResponse.Sort for FIND -- NOT the
    // outer switch's tSort (513). See S05_MyTransfer.cpp:1159-1164 and S04_MyWork04.cpp:1319.
    private const int GmDataTag = 1;

    [Fact]
    public async Task HandleFindAsync_CallerNotBasicTier_AbortsWithNoReply()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Anyone" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleFindAsync(data, session, state, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleFindAsync_TargetFoundOnAnotherMap_ReportsTargetMapId_ThenAcksSuccess()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1, 2]);
        var callerZone = registry[1];
        var targetZone = registry[2];
        var (session, pipe, state) = GmBasicTestSupport.Enter(callerZone, CallerId, "TheGm", 1);
        GmBasicTestSupport.Enter(targetZone, TargetId, "Wanderer");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleFindAsync(data, session, state, CancellationToken.None), callerZone);

        Assert.Null(session.DisconnectReason);
        await GmBasicTestSupport.AssertExactSequenceAsync(pipe,
            new GmCommandResponse { Sort = GmDataTag, GmData = GmBasicTestSupport.MapIdGmData(2) },
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Theory]
    [InlineData("NobodyHome")]
    [InlineData("TheGm")] // self-targeting collapses to not-found (SearchAvatar's own default exclusion)
    public async Task HandleFindAsync_TargetNotFoundOrSelf_ReportsZeroFilledGmData_ButStillAcksSuccess(
        string targetName)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = targetName }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleFindAsync(data, session, state, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await GmBasicTestSupport.AssertExactSequenceAsync(pipe,
            new GmCommandResponse { Sort = GmDataTag, GmData = GmBasicTestSupport.ZeroGmData() },
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }

    // Regression: legacy always writes the literal tag 1 into GmCommandResponse.Sort for FIND, never the
    // outer switch's tSort case number (513) -- S05_MyTransfer.cpp:1159-1164, S04_MyWork04.cpp:1319.
    [Fact]
    public async Task HandleFindAsync_GmCommandResponseSort_IsLegacyLiteralTagOne_NotTheTSortCaseNumber()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1, 2]);
        var callerZone = registry[1];
        var targetZone = registry[2];
        var (session, pipe, state) = GmBasicTestSupport.Enter(callerZone, CallerId, "TheGm", 1);
        GmBasicTestSupport.Enter(targetZone, TargetId, "Wanderer");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleFindAsync(data, session, state, CancellationToken.None), callerZone);

        await GmBasicTestSupport.AssertExactSequenceAsync(pipe,
            new GmCommandResponse { Sort = 1, GmData = GmBasicTestSupport.MapIdGmData(2) },
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

// CALL (514) -- summons a named target (process-local, self-exclusion applies) to the invoker's own
// position (S04_MyWork04.cpp:1324-1384). Only the ordinary single-target branch is implemented -- see
// IGmBasicCommandService's own remarks for the special-server mass-summon branch this project omits.
public class GmBasicCallServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int TargetAccountId = 200;
    private const int Sort = 514;

    // Legacy B_GM_COMMAND_INFO(2, ...) literal tag written into GmCommandResponse.Sort for CALL and
    // MOVE-to-target alike -- NOT the outer switch's tSort (514/515). See S05_MyTransfer.cpp:1159-1164
    // and S04_MyWork04.cpp:1348.
    private const int GmDataTag = 2;

    [Fact]
    public async Task HandleCallAsync_CallerNotBasicTier_AbortsWithNoReply_LogsNothing()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Anyone" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleCallAsync(data, session, state, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Theory]
    [InlineData("NobodyHome")]
    [InlineData("TheGm")]
    public async Task HandleCallAsync_TargetNotFoundOrSelf_AcksFailure_LogsNothing(string targetName)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = targetName }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleCallAsync(data, session, state, CancellationToken.None), zone);

        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task
        HandleCallAsync_TargetFound_TeleportsTargetToCaller_SendsTargetCoordinateResponse_LogsAuditRow_AndAcksCaller()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (caller, callerPipe, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        callerState.PosX = 555f;
        callerState.PosY = 2f;
        callerState.PosZ = 777f;
        var (_, targetPipe, targetState) =
            GmBasicTestSupport.Enter(zone, TargetId, "Wanderer", accountId: TargetAccountId);
        ZoneTestKit.DrainOutbound(callerPipe); // target's own Enter-broadcast join packet, not under test
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleCallAsync(data, caller, callerState, CancellationToken.None), zone);

        Assert.Equal(555f, targetState.PosX);
        Assert.Equal(2f, targetState.PosY);
        Assert.Equal(777f, targetState.PosZ);

        // Target isn't its own AOI neighbor, so its own pipe carries exactly this one dedicated frame.
        await PacketAssert.AssertSentAsync(targetPipe,
            new GmCommandResponse
                { Sort = GmDataTag, GmData = GmBasicTestSupport.PackedCoordinateGmData(555f, 2f, 777f) });
        // The caller may also be a now-co-located AOI neighbor of the relocated target, so tolerate a
        // preceding BroadcastAvatarAction frame ahead of this ack (NeighborActionBroadcast, see
        // TribeProgressZoneCommand.NeighborActionBroadcast's own remarks).
        await GmBasicTestSupport.AssertTailFrameAsync(callerPipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)8, logged.EventCode); // GmActionEventCodes.Call (internal, not visible here)
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(GmBasicTestSupport.AccountId, logged.ActorAccountId);
        Assert.Equal(CallerId, logged.ActorCharacterId);
        Assert.Equal(TargetAccountId, logged.TargetAccountId);
        Assert.Equal(TargetId, logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("TargetName=Wanderer", logged.Payload);
    }
}

// Self-teleport-to-target's-position MOVE (515) -- the reverse of CALL, no target-relocation broadcast for
// this variant (S04_MyWork04.cpp:1385-1410).
public class GmBasicMoveToTargetServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int Sort = 515;

    // Legacy B_GM_COMMAND_INFO(2, ...) literal tag written into GmCommandResponse.Sort for MOVE-to-target --
    // the same tag CALL uses, NOT the outer switch's tSort (515). See S05_MyTransfer.cpp:1159-1164 and
    // S04_MyWork04.cpp:1406.
    private const int GmDataTag = 2;

    [Fact]
    public async Task HandleMoveToTargetAsync_CallerNotBasicTier_AbortsWithNoReply()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Anyone" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleMoveToTargetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData("NobodyHome")]
    [InlineData("TheGm")]
    public async Task HandleMoveToTargetAsync_TargetNotFoundOrSelf_AcksFailure_AndLeavesPositionUnchanged(
        string targetName)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var originalX = state.PosX;
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = targetName }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleMoveToTargetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(originalX, state.PosX);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task
        HandleMoveToTargetAsync_TargetFound_WarpsCallerToTarget_SendsCoordinateResponseThenAck_NoBroadcast()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (caller, callerPipe, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, targetPipe, targetState) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer");
        targetState.PosX = 111f;
        targetState.PosY = 3f;
        targetState.PosZ = 222f;
        ZoneTestKit.DrainOutbound(callerPipe); // target's own Enter-broadcast join packet, not under test
        ZoneTestKit.DrainOutbound(targetPipe);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleMoveToTargetAsync(data, caller, callerState, zone, CancellationToken.None), zone);

        Assert.Equal(111f, callerState.PosX);
        Assert.Equal(3f, callerState.PosY);
        Assert.Equal(222f, callerState.PosZ);

        await GmBasicTestSupport.AssertExactSequenceAsync(callerPipe,
            new GmCommandResponse
                { Sort = GmDataTag, GmData = GmBasicTestSupport.PackedCoordinateGmData(111f, 3f, 222f) },
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        // "no such broadcast is issued for that sibling command" -- unlike CALL's NeighborActionBroadcast.
        PacketAssert.AssertNothingSent(targetPipe);
    }
}

// NCHAT(516)/YCHAT(517) -- marks a named target's (process-local, self-exclusion applies) "special state"
// marker to 2/0, one shared audit-log point for both (S04_MyWork04.cpp:1411-1468). Asymmetric acknowledgment:
// target-not-found falls through to the shared closing step and acks failure (legacy `break`,
// S04_MyWork04.cpp:1429-1432/1458-1461), while target-found sends NO acknowledgment of any kind (legacy
// explicit `return`, S04_MyWork04.cpp:1433-1439/1462-1468).
public class GmBasicTargetSpecialStateServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int TargetAccountId = 200;
    private const int NchatSort = 516;
    private const int YchatSort = 517;

    [Fact]
    public async Task HandleTargetSpecialStateAsync_CallerNotBasicTier_AbortsWithNoReply_LogsNothing()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Anyone" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTargetSpecialStateAsync(NchatSort, data, session, state, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    // Regression: legacy's not-found branch falls through to the shared closing code (a bare `break`, not
    // `return`) and sends a failure GenericActionResponse -- only the target-FOUND branch is truly silent.
    // S04_MyWork04.cpp:1429-1432 (NCHAT not-found `break`), :1458-1461 (YCHAT not-found `break`),
    // :2116-2122 (shared closing code any `break` falls into).
    [Theory]
    [InlineData("NobodyHome")]
    [InlineData("TheGm")]
    public async Task HandleTargetSpecialStateAsync_TargetNotFoundOrSelf_AcksFailure_NoLog(string targetName)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = targetName }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTargetSpecialStateAsync(NchatSort, data, session, state, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = NchatSort, Data = data, RuneValue = 0 });
    }

    // Same asymmetry, YCHAT side (shares the exact same not-found handling as NCHAT).
    [Theory]
    [InlineData("NobodyHome")]
    [InlineData("TheGm")]
    public async Task HandleTargetSpecialStateAsync_Ychat_TargetNotFoundOrSelf_AcksFailure_NoLog(string targetName)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = targetName }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTargetSpecialStateAsync(YchatSort, data, session, state, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = YchatSort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleTargetSpecialStateAsync_Nchat_SetsTargetSpecialStateToTwo_LogsAuditRow_NoAckToAnyone()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (caller, callerPipe, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, targetPipe, targetState) =
            GmBasicTestSupport.Enter(zone, TargetId, "Wanderer", accountId: TargetAccountId);
        ZoneTestKit.DrainOutbound(callerPipe);
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTargetSpecialStateAsync(NchatSort, data, caller, callerState, CancellationToken.None), zone);

        Assert.Equal(2, targetState.SpecialState);
        PacketAssert.AssertNothingSent(callerPipe);
        PacketAssert.AssertNothingSent(targetPipe);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)10, logged.EventCode); // GmActionEventCodes.Chat (internal, not visible here)
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(TargetAccountId, logged.TargetAccountId);
        Assert.Equal(TargetId, logged.TargetCharacterId);
        Assert.Equal((byte)2, logged.Outcome);
        Assert.Equal("TargetName=Wanderer", logged.Payload);
    }

    [Fact]
    public async Task HandleTargetSpecialStateAsync_Ychat_SetsTargetSpecialStateToZero_LogsAuditRow_NoAckToAnyone()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (caller, callerPipe, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, targetPipe, targetState) =
            GmBasicTestSupport.Enter(zone, TargetId, "Wanderer", accountId: TargetAccountId);
        targetState.SpecialState = 1;
        ZoneTestKit.DrainOutbound(callerPipe);
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTargetSpecialStateAsync(YchatSort, data, caller, callerState, CancellationToken.None), zone);

        Assert.Equal(0, targetState.SpecialState);
        PacketAssert.AssertNothingSent(callerPipe);
        PacketAssert.AssertNothingSent(targetPipe);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)0, logged.Outcome);
    }
}

// KICK (518) -- disconnects a named target's (process-local, self-exclusion applies) session, audit-logged
// before the disconnect (S04_MyWork04.cpp:1469-1486). No dedicated response to anyone.
public class GmBasicKickServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int TargetAccountId = 200;
    private const int Sort = 518;

    [Fact]
    public async Task HandleKickAsync_CallerNotBasicTier_AbortsWithNoReply_LogsNothing_AndLeavesTargetConnected()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        var (target, targetPipe, _) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer");
        ZoneTestKit.DrainOutbound(pipe); // target's own Enter-broadcast join packet, not under test
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleKickAsync(data, session, state, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Null(target.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Theory]
    [InlineData("NobodyHome")]
    [InlineData("TheGm")]
    public async Task HandleKickAsync_TargetNotFoundOrSelf_AcksFailure_LogsNothing(string targetName)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = targetName }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleKickAsync(data, session, state, CancellationToken.None), zone);

        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleKickAsync_TargetFound_DisconnectsTarget_LogsAuditRow_AcksCallerSuccess()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (caller, callerPipe, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (target, targetPipe, _) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer", accountId: TargetAccountId);
        ZoneTestKit.DrainOutbound(callerPipe);
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), eventLog,
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmTargetNamePayload { TargetName = "Wanderer" }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleKickAsync(data, caller, callerState, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.GmKicked, target.DisconnectReason);
        PacketAssert.AssertNothingSent(targetPipe);
        await PacketAssert.AssertSentAsync(callerPipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)9, logged.EventCode); // GmActionEventCodes.Kick (internal, not visible here)
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(TargetAccountId, logged.TargetAccountId);
        Assert.Equal(TargetId, logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("TargetName=Wanderer", logged.Payload);
    }
}

// TRIBEBANK (520) -- dead code behind a live tier gate; every statement that would validate input and apply
// an effect exists only as inactive commentary in the shipped legacy source. Always the default-failure
// outcome, no mutation of any kind.
public class GmBasicTribeBankServiceTests
{
    private const int CharacterId = 10;
    private const int Sort = 520;

    [Fact]
    public async Task HandleTribeBankAsync_CallerNotBasicTier_AbortsWithNoReply()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CharacterId, "NotAGm");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTribeBankAsync(data, session, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleTribeBankAsync_BasicTier_DeadCodeBehindLiveGate_AlwaysAcksFailure()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleTribeBankAsync(data, session, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

// LEVEL (521) -- self-only total-level set, decomposed into base level (up to 145) / "high level" (a further
// 12) / rebirth count (a further 12), 169 combined capacity (S04_MyWork04.cpp:1541-1596). Unconditionally
// recomputes derived stats and fully heals the invoker on success. No audit-log entry, matching legacy.
public class GmBasicLevelSetServiceTests
{
    private const int CharacterId = 10;
    private const int Sort = 521;
    private const short BaseLevelRow = 50;
    private const short CapLevelRow = 145;

    private static WorldDataCache LevelWorldData()
    {
        var levelsByLevel = new Dictionary<short, LevelRowDto>
        {
            [BaseLevelRow] = new(BaseLevelRow, 12345, 54321, 0, 0, 0, 0, 0, 0, 0, 0),
            [CapLevelRow] = new(CapLevelRow, 90000, 99999, 0, 0, 0, 0, 0, 0, 0, 0)
        }.ToFrozenDictionary();
        return ZoneTestKit.EmptyWorldData(levelsByLevel: levelsByLevel);
    }

    [Fact]
    public async Task HandleLevelSetAsync_CallerNotBasicTier_AbortsWithNoReply_AndLeavesLevelUnchanged()
    {
        var worldData = LevelWorldData();
        var (registry, zone) = GmBasicTestSupport.CreateWorld(worldData);
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "NotAGm");
        var originalLevel = state.Level;
        var service = new GmBasicCommandService(registry, worldData, new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmLevelSetPayload { Level = BaseLevelRow }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleLevelSetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal(originalLevel, state.Level);
    }

    [Fact]
    public async Task HandleLevelSetAsync_BaseTierRequest_SetsLevelAndExperience_FullyHeals_AcksSuccess()
    {
        var worldData = LevelWorldData();
        var (registry, zone) = GmBasicTestSupport.CreateWorld(worldData);
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, worldData, new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmLevelSetPayload { Level = BaseLevelRow }.Write(d));

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleLevelSetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(BaseLevelRow, state.Level);
        Assert.Equal(0, state.Level2);
        Assert.Equal(0, state.RebirthCount);
        Assert.Equal(12345, state.Experience); // level 50's own ExpRangeMin
        Assert.Equal(0, state.Exp2); // plain base-level tier carries no high-level/rebirth component
        Assert.Equal(state.MaxLife, state.Life); // "always fully heals the invoker"
        Assert.Equal(state.MaxMana, state.Mana);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleLevelSetAsync_HighLevelTierRequest_DecomposesIntoBaseCapPlusLevel2()
    {
        var worldData = LevelWorldData();
        var (registry, zone) = GmBasicTestSupport.CreateWorld(worldData);
        var (session, _, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, worldData, new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmLevelSetPayload { Level = 150 }.Write(d)); // 145 + 5

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleLevelSetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(CapLevelRow, state.Level);
        Assert.Equal(5, state.Level2);
        Assert.Equal(0, state.RebirthCount);
        Assert.Equal(99999, state.Experience); // maxBaseExperience, level 145's own ExpRangeMax
        // Regression: wAvatar.aExp2 = mLEVEL.ReturnHighExpValue(wAvatar.aLevel2) -- RebirthProgression.
        // HighLevelExpTable[Level2-1], NOT the previously-written flagged-gap 0.
        Assert.Equal(RebirthProgression.HighLevelExpTable[4], state.Exp2);
    }

    [Fact]
    public async Task HandleLevelSetAsync_RebirthTierRequest_CapsLevelAndLevel2_IncrementsRebirthCount()
    {
        var worldData = LevelWorldData();
        var (registry, zone) = GmBasicTestSupport.CreateWorld(worldData);
        var (session, _, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, worldData, new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmLevelSetPayload { Level = 165 }.Write(d)); // 145+12+8

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleLevelSetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(CapLevelRow, state.Level);
        Assert.Equal(12, state.Level2);
        Assert.Equal(8, state.RebirthCount);
        Assert.Equal(99999, state.Experience);
        // Regression: wAvatar.aExp2 = mLEVEL.ReturnHighExpValue(MAX_LIMIT_HIGH_LEVEL_NUM) for the rebirth
        // tier (Level2 pinned at its own cap) -- RebirthProgression.HighLevelExpTable[MaxHighLevel-1], NOT
        // the previously-written flagged-gap 0.
        Assert.Equal(RebirthProgression.HighLevelExpTable[RebirthProgression.MaxHighLevel - 1], state.Exp2);
    }

    [Fact]
    public async Task HandleLevelSetAsync_AboveCombinedCapacity_RejectedWithNoMutation()
    {
        var worldData = LevelWorldData();
        var (registry, zone) = GmBasicTestSupport.CreateWorld(worldData);
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var originalLevel = state.Level;
        var service = new GmBasicCommandService(registry, worldData, new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData(d => new GmLevelSetPayload { Level = 170 }.Write(d)); // > 169

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleLevelSetAsync(data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(originalLevel, state.Level);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

// STR/DEX/VIT/INT stat edit (522) -- dead code behind a live tier gate, same posture as TRIBEBANK(520).
// Always the default-failure outcome, no mutation of any kind.
public class GmBasicStatEditServiceTests
{
    private const int CharacterId = 10;
    private const int Sort = 522;

    [Fact]
    public async Task HandleStatEditAsync_CallerNotBasicTier_AbortsWithNoReply()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CharacterId, "NotAGm");
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleStatEditAsync(data, session, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleStatEditAsync_BasicTier_DeadCodeBehindLiveGate_AlwaysAcksFailure()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CharacterId, "TheGm", 1);
        var service = new GmBasicCommandService(registry, ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(),
            NullLogger<GmBasicCommandService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleStatEditAsync(data, session, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
    }
}
