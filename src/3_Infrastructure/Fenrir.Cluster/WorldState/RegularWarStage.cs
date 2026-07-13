namespace Fenrir.Cluster.WorldState;

/// <summary>
/// Internal, finer-grained lifecycle stage of one Regular War (Zone049) instance, as driven by the
/// Center authority. Several stages map to the same published <c>mZone049TypeState</c> value
/// (the whole <c>0</c> "rest/cooldown" band is three internal stages); the published projection lives in
/// <see cref="RegularWarPhasePlan.PublishedStateOf"/>.
/// </summary>
/// <remarks>
/// This enum is richer than the wire-visible phase byte on purpose: the Center must know <em>which</em>
/// wall-clock timer is currently running (30 min cooldown vs. 10 min announce countdown vs. 1 min
/// confirmation) even though all three publish the same <c>0</c> to the shard. The published byte is the
/// only thing a shard ever sees.
/// </remarks>
public enum RegularWarStage : byte
{
    /// <summary>Post-war rest window (published <c>0</c>). Entry is closed. 30 min.</summary>
    Cooldown = 0,

    /// <summary>Pre-war announce countdown (published <c>0</c>). ~10 min; announces at 10/5/1 min left.</summary>
    AnnounceCountdown = 1,

    /// <summary>Confirmation window after the countdown (published <c>0</c>). 1 min.</summary>
    PreOpenConfirmation = 2,

    /// <summary>Entry window: joining the RW map is allowed (published <c>1</c>). 3 min.</summary>
    EntryWindow = 3,

    /// <summary>Gates closed, pre-war gathering (published <c>2</c>). 1 min.</summary>
    Gathering = 4,

    /// <summary>WAR ACTIVE: PvP + scoring + drops on (published <c>3</c>). 15 min.</summary>
    WarActive = 5,

    /// <summary>After-war return-to-town window (published <c>4</c>). 1.5 min.</summary>
    ReturnToTown = 6,

    /// <summary>Closing / eviction of remaining players (published <c>5</c>). ~6 s, then back to cooldown.</summary>
    Closing = 7
}
