using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// GM-BLOCK (Fenrir's dedicated wire command; legacy case 519 "[GM]-BLOCK",
// Server/ts25zone/S04_MyWork04.cpp:1487-1515) -- the three legacy outcomes are asymmetric and must stay that
// way: unauthorized -> disconnect with no reply; not found (including self-target) -> the shared opcode-23
// GenericActionResponse ack (Sort=519, legacy's own real reply shape, S04_MyWork04.cpp:2121-2122), never a
// dedicated message; success -> silence (no ack at all), only the target is disconnected.
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
        var service = new GmBlockAvatarService(registry, bans, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarRequest { AvatarName = "AnyoneAtAll" }, caller,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, caller.DisconnectReason);
        PacketAssert.AssertNothingSent(callerPipe);
        Assert.Null(bans.LastCreatedBan);
    }

    [Fact]
    public async Task HandleAsync_GmButTargetNotOnline_SendsGenericFailureAck_AndDoesNotDisconnectCaller()
    {
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, _) = Enter(zone, 10, "TheGm", 1);
        var bans = new FakeBanRepository();
        var service = new GmBlockAvatarService(registry, bans, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarRequest { AvatarName = "NobodyOnline" }, caller,
            CancellationToken.None);

        Assert.Null(caller.DisconnectReason);
        Assert.Null(bans.LastCreatedBan);
        await PacketAssert.AssertSentAsync(callerPipe, new GenericActionResponse
        {
            Result = 1, Sort = GmBlockSort, Data = EmptyGenericActionData, RuneValue = 0
        });
    }

    [Fact]
    public async Task HandleAsync_GmTargetsOwnName_TreatedIdenticallyToNotFound()
    {
        // Legacy SearchAvatar's own default (tWithSameIndex=FALSE) excludes the caller's own index.
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, _) = Enter(zone, 10, "TheGm", 1);
        var bans = new FakeBanRepository();
        var service = new GmBlockAvatarService(registry, bans, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarRequest { AvatarName = "TheGm" }, caller, CancellationToken.None);

        Assert.Null(caller.DisconnectReason);
        Assert.Null(bans.LastCreatedBan);
        await PacketAssert.AssertSentAsync(callerPipe, new GenericActionResponse
        {
            Result = 1, Sort = GmBlockSort, Data = EmptyGenericActionData, RuneValue = 0
        });
    }

    [Fact]
    public async Task HandleAsync_GmValidTarget_CreatesTheBan_DisconnectsTheTargetSilently_AndSendsNoAckToTheCaller()
    {
        var (registry, zone) = CreateWorld();
        var (caller, callerPipe, _) = Enter(zone, 10, "TheGm", 1);
        var (target, targetPipe, targetState) = Enter(zone, 20, "Griefer", 0, 200);
        ZoneTestKit.DrainOutbound(
            callerPipe); // target's own Enter-broadcast join packet reaching the GM, not under test
        var bans = new FakeBanRepository();
        var service = new GmBlockAvatarService(registry, bans, NullLogger<GmBlockAvatarService>.Instance);

        await service.HandleAsync(new GmBlockAvatarRequest { AvatarName = "Griefer" }, caller, CancellationToken.None);

        Assert.NotNull(bans.LastCreatedBan);
        var ban = bans.LastCreatedBan!.Value;
        Assert.Equal(200, ban.AccountId);
        Assert.Equal(targetState.CharacterId, ban.CharacterId);
        Assert.Equal(BanReason.GmManualBlock, ban.Reason);
        Assert.NotNull(ban.ExpiresAtUtc);
        Assert.True(ban.ExpiresAtUtc >
                    DateTime.UtcNow.AddYears(29)); // "today plus 365*30 days," a concrete future date

        Assert.Equal(DisconnectReason.Banned, target.DisconnectReason);

        // Silence is the "it worked" signal on this path -- legacy case 519 `return`s immediately, bypassing
        // the shared epilogue every sibling sub-command (e.g. case 518 KICK) reaches via `break`.
        Assert.Null(caller.DisconnectReason);
        PacketAssert.AssertNothingSent(callerPipe);
    }
}
