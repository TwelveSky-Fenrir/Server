using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone" />'s mutual-visibility step of <c>Enter</c>: new arrival and existing neighbors must
///     each learn about the other exactly once.
/// </summary>
public class ZoneEnterTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    [Fact]
    public void Enter_NewArrivalLearnsAboutEachPreExistingNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // same AOI cell as A (cell size 75, both floor to (0, 0))
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var bInbox = ZoneTestKit.DrainOutbound(pipeB);
        Assert.Equal(OneFrame, bInbox.Length);
    }

    [Fact]
    public void Enter_PreExistingNeighborLearnsAboutTheNewArrival_ExactlyOnce()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var aInbox = ZoneTestKit.DrainOutbound(pipeA);
        Assert.Equal(OneFrame, aInbox.Length);
    }

    /// <summary>
    ///     The race this closes: a relogin's Enter for a characterId reaches this zone's single-writer tick
    ///     thread while a DIFFERENT (stale) session is still tracked under that same characterId -- because
    ///     that stale session's own disconnect teardown (which is what posts its Leave) hasn't run yet. The
    ///     newer session must win (it already passed the cross-process AccountSessions authority check before
    ///     ever reaching Zone), and the stale one must be evicted rather than the new Enter being silently
    ///     dropped and leaving the new session an untracked zombie.
    /// </summary>
    [Fact]
    public void Enter_StalePriorSessionForSameCharacter_IsEvictedAndReplacedByTheNewerSession()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (staleSession, stalePipe) = ZoneTestKit.CreateSession(1);
        var (newSession, _) = ZoneTestKit.CreateSession(2);

        // Mirrors what GameConnectionHost/EnterWorldService set once a session's own registration completes --
        // needed so this test can observe the eviction branch's own CurrentZone=null side effect.
        staleSession.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(staleSession, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(stalePipe);

        // The relogin: same characterId, a brand-new session, while the stale one above is still tracked.
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(newSession, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Evicted, staleSession.DisconnectReason);
        Assert.Null(staleSession.CurrentZone);

        // The zone tracks exactly one PlayerRuntimeState for characterId 10, and it is the NEWER session's own
        // (position 20/20 from the second Enter, not the stale 10/10 from the first) -- a truly duplicate/
        // ignored Enter would have left the STALE state (and session reference) behind instead.
        Assert.True(zone.TryGetPlayer(10, out var tracked));
        Assert.Same(newSession, tracked!.Session);
        Assert.Equal(20f, tracked.PosX);
        Assert.Equal(20f, tracked.PosZ);
    }

    /// <summary>
    ///     A true duplicate Enter command for a session ALREADY tracked here (same session reference, not a
    ///     newer one racing a stale prior one) must keep failing safe exactly as before: ignored, not evicted,
    ///     not re-applied. This is what distinguishes the eviction branch above from an ordinary programming-error
    ///     double-post of the same command for the same still-live session.
    /// </summary>
    [Fact]
    public void Enter_TrueDuplicateForTheSameSession_IsIgnored_NotEvicted()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        // Same session, re-posted (e.g. a spurious replay) -- must not evict itself.
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Same(zone, session.CurrentZone);

        // The FIRST Enter's state stands -- the duplicate is ignored outright, never applied.
        Assert.True(zone.TryGetPlayer(10, out var tracked));
        Assert.Equal(10f, tracked!.PosX);
        Assert.Equal(10f, tracked.PosZ);
    }
}
