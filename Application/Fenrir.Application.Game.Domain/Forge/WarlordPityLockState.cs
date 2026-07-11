namespace Fenrir.Application.Game.Domain.Forge;

/// <summary>
///     Process-wide (zone-shard-wide, NOT per-character or per-account) anti-jackpot lock for the High-Item
///     "Warlord reroll" swap (workstream C12-warlord-chest, "part B") -- mirrors legacy's own
///     <c>mGoodWarlord2</c> file-scope flag (Server/ts25zone/S07_MyGame03.cpp:5425), which is read once at
///     the top of every <c>ReturnWarlordForUpgrade</c> call to decide whether to force the random draw to
///     exactly 50 instead of taking a fresh roll (<c>:5428-5433</c>), and is set true by any of the eight
///     elite-tier 1% "top-tier" branches (<c>:5554,5565,5576,5594,5612,5632,5652,5672</c>). A forced draw of
///     50 is above every threshold in both reroll tables except the rare-tier armor slot's 41%-band upper
///     bound (still false: 50 is not &lt; 46) -- so once engaged, BOTH the rare-tier 5% top-tier outcome and
///     the elite-tier 1% top-tier outcome become unreachable for every character on this zone process, of
///     any tribe, until the process restarts. Never reset by any legacy code path (a repo-wide search found
///     no assignment back to false anywhere in <c>Server/</c>) -- this is intentionally NOT modeled with a
///     <c>Reset()</c> method; adding one would be inventing behavior no cited legacy path exhibits.
/// </summary>
/// <remarks>
///     Intended as a DI singleton, one instance per <c>GameServer</c> shard process (legacy's flag is scoped
///     to whichever single zone-process instance handles the request -- Fenrir's closest equivalent, since
///     <c>GameServer</c> is sharded by map, not by a single global process; see this workstream's own
///     contract "Critical, non-obvious side effect" note flagging this as a genuinely surprising
///     shared-mutable-state mechanic requiring an explicit downstream design decision, not a silent port).
///     <see cref="Engaged" /> is a plain <c>bool</c> field with no locking: legacy's own flag has no
///     synchronization either (a single-threaded zone process), and Fenrir's write side
///     (<see cref="Engage" />, called only from <see cref="WarlordRerollBonusTable.TryDrawReplacement" />) is
///     always invoked from whichever single thread is resolving a given character's upgrade request through
///     this shard's own service call path -- not the zone tick thread. If a future caller needs to read/write
///     this from multiple concurrent threads within one shard process, mark the field <c>volatile</c> at that
///     point (same posture as <c>Fenrir.Application.Game.Domain.Gm.YangGokPvpDropEventState</c>'s own
///     remarks on when to add that); not added preemptively here since no such caller exists yet.
/// </remarks>
public sealed class WarlordPityLockState
{
    /// <summary>
    ///     The forced draw value once <see cref="Engaged" /> is true (Server/ts25zone/S07_MyGame03.cpp:5428-5433,
    ///     <c>rand_mir() % 100</c> replaced with the literal 50). Exposed as a public constant so
    ///     <see cref="WarlordRerollBonusTable" /> and tests share one source of truth.
    /// </summary>
    public const int ForcedDrawValue = 50;

    /// <summary>Whether the lock has been engaged (legacy <c>mGoodWarlord2</c>). Never resets to false.</summary>
    public bool Engaged { get; private set; }

    /// <summary>Engages the lock. Idempotent -- engaging an already-engaged lock is a no-op.</summary>
    public void Engage()
    {
        Engaged = true;
    }
}
