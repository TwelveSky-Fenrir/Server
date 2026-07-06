using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.HandleEnter" />'s unconditional <see cref="DuelRegistry.ForceClearOnZoneEntry" />
///     call -- stale duel-related state (from a persisted snapshot, or a fast disconnect/reconnect race that
///     beat <see cref="Fenrir.Application.Game.Domain.Simulation.DuelMaintenanceSystem" />'s own tick-based
///     cleanup) must never survive into a fresh zone entry, independent of the tick-based end conditions.
/// </summary>
public class ZoneDuelEntryTests
{
    [Fact]
    public void Entry_WithStaleActiveDuelState_ClearsTheEnteringCharactersOwnKey()
    {
        var duels = new DuelRegistry();
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false));
        Assert.True(duels.TryAnswer(20, true, out _));
        Assert.True(duels.TryStart(10, out _)); // 10 now looks like it's mid-Active-duel before ever entering

        var zone = ZoneTestKit.CreateZone(1, duelRegistry: duels);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(duels.TryGetActiveDuel(10, out _));

        // 10 can immediately ask someone new -- the stale flag no longer forces a disconnect.
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 30, false));
    }

    [Fact]
    public void Entry_WithStalePendingAsk_ClearsBothDirections()
    {
        var duels = new DuelRegistry();
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false)); // 20 never answers

        var zone = ZoneTestKit.CreateZone(1, duelRegistry: duels);
        var (session, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, 1, name: "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Neither the entering target nor the original (now-abandoned) challenger is left dangling -- two
        // DISTINCT new targets, since asking the same third party from both would itself produce a genuine
        // TargetBusy (the second ask's target would already be negotiating with the first asker), which has
        // nothing to do with the stale-state cleanup this test is isolating.
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
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw

        Assert.True(zone.TryGetPlayer(10, out _));
    }
}
