using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

public class GmBlockAvatarServiceTests
{
    private const int GmBlockSort = 519;

    private const short MapId = 1;
    private static readonly byte[] EmptyGenericActionData = new byte[130];

    private static (ZoneRegistry Registry, Zone Zone) CreateWorld()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([MapId]);
        return (registry, registry[MapId]);
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Enter(Zone zone,
        int characterId, string name, short accountGrade = 0, int accountId = 100)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(accountId, characterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, name)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Fact]
    public async Task HandleAsync_CallerNotGm_AbortsWithNoReply_AndCreatesNoBan()
    {
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, _) = Enter(zone, 10, "NotAGm");
        var bans = new FakeBanRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmBlockAvatarService(registry, bans, eventLog, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarPayload { AvatarName = "AnyoneAtAll" }, caller,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, caller.DisconnectReason);
        PacketAssert.AssertNothingSent(callerPipe);
        Assert.Null(bans.LastCreatedBan);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_GmButTargetNotOnline_SendsGenericFailureAck_AndDoesNotDisconnectCaller()
    {
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, _) = Enter(zone, 10, "TheGm", 1);
        var bans = new FakeBanRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmBlockAvatarService(registry, bans, eventLog, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarPayload { AvatarName = "NobodyOnline" }, caller,
            CancellationToken.None);

        Assert.Null(caller.DisconnectReason);
        Assert.Null(bans.LastCreatedBan);
        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(callerPipe, new GenericActionResponse
        {
            Result = 1, Sort = GmBlockSort, Data = EmptyGenericActionData, RuneValue = 0
        });
    }

    [Fact]
    public async Task HandleAsync_GmTargetsOwnName_TreatedIdenticallyToNotFound()
    {
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, _) = Enter(zone, 10, "TheGm", 1);
        var bans = new FakeBanRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmBlockAvatarService(registry, bans, eventLog, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarPayload { AvatarName = "TheGm" }, caller, CancellationToken.None);

        Assert.Null(caller.DisconnectReason);
        Assert.Null(bans.LastCreatedBan);
        Assert.Empty(eventLog.LoggedEvents);
        await PacketAssert.AssertSentAsync(callerPipe, new GenericActionResponse
        {
            Result = 1, Sort = GmBlockSort, Data = EmptyGenericActionData, RuneValue = 0
        });
    }

    [Fact]
    public async Task HandleAsync_GmValidTarget_CreatesTheBan_DisconnectsTheTargetSilently_AndSendsNoAckToTheCaller()
    {
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, callerState) = Enter(zone, 10, "TheGm", 1);
        var (target, targetPipe, targetState) = Enter(zone, 20, "Griefer", 0, 200);
        ZoneTestKit.DrainOutbound(
            callerPipe);
        var bans = new FakeBanRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmBlockAvatarService(registry, bans, eventLog, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarPayload { AvatarName = "Griefer" }, caller, CancellationToken.None);

        Assert.NotNull(bans.LastCreatedBan);
        var ban = bans.LastCreatedBan!.Value;
        Assert.Equal(200, ban.AccountId);
        Assert.Equal(targetState.CharacterId, ban.CharacterId);
        Assert.Equal(BanReason.GmManualBlock, ban.Reason);
        Assert.NotNull(ban.ExpiresAtUtc);
        Assert.True(ban.ExpiresAtUtc >
                    DateTime.UtcNow.AddYears(29));

        Assert.Equal(DisconnectReason.Banned, target.DisconnectReason);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)11, logged.EventCode);
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(caller.AccountId, logged.ActorAccountId);
        Assert.Equal(callerState.CharacterId, logged.ActorCharacterId);
        Assert.Equal(200, logged.TargetAccountId);
        Assert.Equal(targetState.CharacterId, logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("TargetName=Griefer", logged.Payload);

        Assert.Null(caller.DisconnectReason);
        PacketAssert.AssertNothingSent(callerPipe);
    }
}
