using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests;

public class AccountSessionKickPollHostTests
{
    private const byte ShardId = 3;
    private const short MapId = 1;

    [Fact]
    public async Task PollOnceAsync_NoLocalSessions_SkipsTheRoundTripEntirely()
    {
        var registry = new SessionRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        var accountSessions = new FakeAccountSessionRepository();
        var host = CreateHost(registry, zones, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Empty(accountSessions.RefreshCalls);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_AbortsTheLiveSession_AndClearsTheOwnershipRow()
    {
        const int accountId = 42;
        const int characterId = 10;
        var registry = new SessionRegistry();
        var (session, _, zones) = CreateReadyZoneSession(accountId, characterId);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, accountId);

        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, zones, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Equal(DisconnectReason.Evicted, session.DisconnectReason);
        var refreshCall = Assert.Single(accountSessions.RefreshCalls);
        Assert.Equal(AccountSessionServerKind.Game, refreshCall.ServerKind);
        Assert.Equal(ShardId, refreshCall.ShardId);
        Assert.Contains(accountId, refreshCall.AccountIds);
        var cleared = Assert.Single(accountSessions.ClearedOwners);
        Assert.Equal((accountId, AccountSessionServerKind.Game, (byte?)ShardId, kickToken), cleared);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_SendsLoginFromAnotherNoticeTwice_BeforeAborting()
    {
        const int accountId = 42;
        const int characterId = 10;
        var registry = new SessionRegistry();
        var (session, pipe, zones) = CreateReadyZoneSession(accountId, characterId);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, accountId);

        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, zones, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        var expected = new AvatarStatUpdateResponse { Sort = 903, Value = 0, Value2 = 0 };
        var frame = new byte[FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>()];
        FrameWriter.WriteFrame(in expected, frame);
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        Assert.Equal(frame.Concat(frame).ToArray(), actual);
        Assert.Equal(DisconnectReason.Evicted, session.DisconnectReason);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_ButNoLongerHeldLocally_StillClearsTheRow_DoesNotThrow()
    {
        const int accountId = 99;
        var registry = new SessionRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, zones, accountSessions);

        registry.Register(ZoneTestKit.CreateSession(2).Session);
        registry.AssociateAccount(2, 1);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Single(accountSessions.ClearedOwners, o => o.AccountId == accountId);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_SessionNotYetInWorld_IsSkipped_ButRowIsStillCleared()
    {
        const int accountId = 42;
        var registry = new SessionRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, accountId);

        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, zones, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Single(accountSessions.ClearedOwners, o => o.AccountId == accountId);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_SessionMidZoneTransfer_IsSkipped_ButRowIsStillCleared()
    {
        const int accountId = 42;
        const int characterId = 10;
        var registry = new SessionRegistry();
        var (session, pipe, zones) = CreateReadyZoneSession(accountId, characterId, isMovingZone: true);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, accountId);

        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, zones, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Single(accountSessions.ClearedOwners, o => o.AccountId == accountId);
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, ZoneRegistry Zones) CreateReadyZoneSession(
        int accountId, int characterId, bool isMovingZone = false)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(accountId, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([MapId]);
        zones[MapId].Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, MapId)));
        zones[MapId].Tick(TimeSpan.FromMilliseconds(50));

        if (isMovingZone)
        {
            Assert.True(zones.TryGetPlayer(characterId, out var state));
            state!.IsMovingZone = true;
        }

        return (session, pipe, zones);
    }

    private static AccountSessionKickPollHost CreateHost(SessionRegistry registry, ZoneRegistry zones,
        FakeAccountSessionRepository accountSessions)
    {
        return new AccountSessionKickPollHost(registry, zones, accountSessions,
            Options.Create(new GameServerOptions { ShardId = ShardId }),
            NullLogger<AccountSessionKickPollHost>.Instance);
    }
}
