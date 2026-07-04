using Fenrir.Application.Game.Handlers.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeVoteHandlerTests
{
    private readonly TribeVoteHandler _handler = new();

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 9)]
    [InlineData(3, 5)]
    public void CandidacyOrVote_WithValidSlot_StillAborts_NoElectionWindowExistsInFenrirYet(int sort, int value)
    {
        var (session, _) = ZoneTestKit.CreateSession(1);

        _handler.Handle(new TribeVoteRequest { Sort = sort, Value = value }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(1, 10)]
    [InlineData(3, 999)]
    public void CandidacyOrVote_WithOutOfBoundsSlot_Aborts(int sort, int value)
    {
        var (session, _) = ZoneTestKit.CreateSession(1);

        _handler.Handle(new TribeVoteRequest { Sort = sort, Value = value }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void UnsupportedSort_Aborts(int sort)
    {
        var (session, _) = ZoneTestKit.CreateSession(1);

        _handler.Handle(new TribeVoteRequest { Sort = sort, Value = 0 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }
}
