using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Handlers.Social;

/// <summary>
///     Covers <see cref="DuelAskHandler" />'s dispatch on <see cref="DuelAskResultKind" />, in particular the
///     forced-disconnect edge case for <see cref="DuelAskResultKind.ChallengerAlreadyDueling" /> -- a desynced
///     is-dueling flag on the requester's own side is not answered as an ordinary "busy" rejection like
///     <see cref="DuelAskResultKind.ChallengerBusy" />/<see cref="DuelAskResultKind.TargetBusy" />, it tears
///     down the requester's own session instead (Server/ts25zone/S04_MyWork02.cpp:8259-8263).
/// </summary>
public class DuelAskHandlerTests
{
    private static (DuelAskHandler Handler, ZoneClientSession Session, FakeDuplexPipe Pipe) CreateHandler(
        DuelAskResultKind resultKind)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var handler = new DuelAskHandler(new StubDuelService(resultKind));
        return (handler, session, pipe);
    }

    [Fact]
    public async Task ChallengerAlreadyDueling_AbortsTheRequesterSSession_NotAnOrdinaryBusyReply()
    {
        var (handler, session, pipe) = CreateHandler(DuelAskResultKind.ChallengerAlreadyDueling);

        await handler.HandleAsync(new DuelChallengeRequest { AvatarName = "Target", Sort = 0 }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // no ZC_DUEL_ANSWER_RECV -- the session is just torn down
    }

    [Fact]
    public async Task ChallengerBusy_OrdinaryNegotiatingCase_RepliesInstead_DoesNotDisconnect()
    {
        var (handler, session, pipe) = CreateHandler(DuelAskResultKind.ChallengerBusy);

        await handler.HandleAsync(new DuelChallengeRequest { AvatarName = "Target", Sort = 0 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    private sealed class StubDuelService(DuelAskResultKind resultKind) : IDuelService
    {
        public ValueTask<DuelAskResultKind> AskAsync(Zone zone, PlayerRuntimeState challenger,
            string targetAvatarName, int sort, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(resultKind);
        }

        public void Answer(int targetId, int answerCode)
        {
            throw new NotSupportedException();
        }

        public void Cancel(int challengerId)
        {
            throw new NotSupportedException();
        }

        public void Start(int callerId)
        {
            throw new NotSupportedException();
        }
    }
}
