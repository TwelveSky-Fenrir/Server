using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneDuelEntryTests
{
    [Fact]
    public void Entry_WithStaleActiveDuelState_ClearsTheEnteringCharactersOwnKey()
    {
        var duels = new DuelRegistry();
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false));
        Assert.True(duels.TryAnswer(20, true, out _));
        Assert.True(duels.TryStart(10, out _));

        var zone = ZoneTestKit.CreateZone(1, duelRegistry: duels);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(duels.TryGetActiveDuel(10, out _));

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 30, false));
    }

    [Fact]
    public void Entry_WithStalePendingAsk_ClearsBothDirections()
    {
        var duels = new DuelRegistry();
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false));

        var zone = ZoneTestKit.CreateZone(1, duelRegistry: duels);
        var (session, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(20, 40, false));
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 50, false));
    }

    [Fact]
    public void Entry_WithNoStaleState_IsUnaffected()
    {
        var duels = new DuelRegistry();
        var zone = ZoneTestKit.CreateZone(1, duelRegistry: duels);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out _));
    }
}
