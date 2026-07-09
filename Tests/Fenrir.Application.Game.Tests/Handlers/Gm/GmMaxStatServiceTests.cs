using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Admin-tier GM-MAX stat-cheat (legacy PROCESS_DATA_SEND, opcode 19, tSort 509 -- there is no dedicated
// legacy wire opcode for this action, and it reads no request payload at all --
// Server/ts25zone/S04_MyWork04.cpp:1188-1202). GmMaxStatService's own contract: unconditionally sets
// VIT/STR/INT/DEX=10000 and SkillPoints=3000 on success, NEVER sends a response packet on either the
// gate-fail or success path (both end in a forced full-logout disconnect instead), and -- unlike the cited
// legacy body, which writes no audit row -- Fenrir adds a game.EventLog (Category=GmAction) row as a
// project-decision audit trail for this tier.
public class GmMaxStatServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;

    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GmMaxStatService.HandleAsync never completed.");
        }

        await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeEventLogRepository EventLog) SetUp(short accountGrade)
    {
        var zone = ZoneTestKit.CreateZone(MapId);
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

    [Fact]
    public async Task HandleAsync_CallerNotAdminTier_AbortsWithNoReply_LogsNothing_AndLeavesStatsUnchanged()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmMaxStatService(eventLog, NullLogger<GmMaxStatService>.Instance);

        await RunToCompletionAsync(service.HandleAsync(session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.GmCommandLogout, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(0, state.StatVit);
        Assert.Equal(0, state.StatStr);
        Assert.Equal(0, state.StatInt);
        Assert.Equal(0, state.StatDex);
        Assert.Equal(0, state.SkillPoints);
    }

    [Fact]
    public async Task HandleAsync_AdminTier_SetsAllFourStatsAndSkillPoints_LogsOneAuditRow_AndForcesLogoutWithNoReply()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = new GmMaxStatService(eventLog, NullLogger<GmMaxStatService>.Instance);

        await RunToCompletionAsync(service.HandleAsync(session, state, zone, CancellationToken.None), zone);

        Assert.Equal(10000, state.StatVit);
        Assert.Equal(10000, state.StatStr);
        Assert.Equal(10000, state.StatInt);
        Assert.Equal(10000, state.StatDex);
        Assert.Equal(3000, state.SkillPoints);

        // Legacy's case 509 body bypasses the shared response epilogue entirely -- silence is the "it worked"
        // signal here, same posture as GmBlockAvatarService's success path; only the forced disconnect is
        // observable to the client.
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal(DisconnectReason.GmCommandLogout, session.DisconnectReason);

        var logged = Assert.Single(eventLog.LoggedEvents);
        // GmActionEventCodes is internal to Fenrir.Application.Game.Services (no InternalsVisibleTo to this
        // test project) -- 1 is GmActionEventCodes.MaxStatCheat, the locally-scoped EventCode documented on
        // that type for this exact command.
        Assert.Equal((short)1, logged.EventCode);
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("VIT=STR=INT=DEX=10000;SkillPoints=3000", logged.Payload);
    }
}
