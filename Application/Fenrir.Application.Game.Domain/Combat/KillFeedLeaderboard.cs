using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     One ranked leaderboard row -- carries <see cref="CharacterId" /> (Fenrir's natural analogue of the
///     legacy's fixed 0-999 per-instance tracking-slot index, which Fenrir has no equivalent bounded array
///     for -- see <see cref="KillFeedLeaderboard" />'s own remarks) alongside the name/tribe/kills fields the
///     outgoing kill-feed broadcast actually needs.
/// </summary>
public readonly record struct KillFeedRankedEntry(int CharacterId, string Name, byte Tribe, int Kills)
{
    /// <summary>
    ///     Projects to the wire-facing shape (drops <see cref="CharacterId" />), for
    ///     <see cref="KillFeedBroadcastPayload" />.
    /// </summary>
    public KillFeedTopEntry ToTopEntry()
    {
        return new KillFeedTopEntry(Name, Tribe, Kills);
    }
}

/// <summary>
///     A war zone's persistent, per-instance top-killer leaderboard -- reimplements the legacy 1000-entry
///     store the kill-feed routine inserts/re-sorts on every qualifying enemy kill and the end-of-battle CP
///     reward reads its top three from.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:12-44 (leaderboard-entry structure; top-3 count is 3,
///         capacity is 1000; the entry-clear sets name empty, tribe 0, kills 0, slot-index -1, invalid) ;
///         :307-360 (comparison -- descending by kills only ; insert/update -- existing-by-slot else
///         first-free else full-and-return ; full re-sort ; copy of the top three valid entries' name/tribe/
///         kills, with validity/slot-index deliberately NOT copied into the outgoing feed, and the 13-char
///         name copy not guaranteeing a null terminator at exactly 13 characters).
///     </para>
///     <para>
///         <b>CharacterId instead of a slot index:</b> legacy finds/updates a killer's own entry by a fixed
///         per-session tracking-slot index (a bounded 0-999 array position each connected avatar occupies for
///         the lifetime of its connection). Fenrir has no equivalent fixed-size per-instance slot array
///         (<see cref="World.Zone" /> keys its players by <see cref="World.PlayerRuntimeState.CharacterId" />
///         in a <c>ConcurrentDictionary</c>, not a bounded slot array) -- <see cref="KillFeedRankedEntry.CharacterId" />
///         is therefore the natural, uniquely-identifying substitute for "find the killer's own existing
///         entry," with the same practical effect (one entry per distinct killer, found or created in O(n)
///         over at most <see cref="Capacity" /> rows).
///     </para>
///     <para>
///         <b>Ranking key is the caller-supplied kill total, not an internally-owned counter.</b> This class
///         does not itself track "how many kills has this player made" -- <see cref="RecordKill" /> takes the
///         caller's already-current running total (<see cref="World.PlayerRuntimeState.SessionKillCount" /> in
///         production) and simply records/sorts by it. This is deliberate: the source contract's own Edge
///         case "Ranking key is a session total, not a per-battle total" means the counter this ranks by must
///         survive this leaderboard's own <see cref="Clear" /> (battle open/reset/close) -- see
///         <see cref="World.PlayerRuntimeState.SessionKillCount" />'s own remarks for where that counter
///         actually lives and why it is NOT reset here.
///     </para>
/// </remarks>
public sealed class KillFeedLeaderboard
{
    /// <summary>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:12-44 (fixed 1000-entry array).</summary>
    public const int Capacity = 1000;

    private readonly List<KillFeedRankedEntry> _entries = new(Capacity);

    /// <summary>How many distinct killers currently occupy a slot (0..<see cref="Capacity" />).</summary>
    public int Count => _entries.Count;

    /// <summary>
    ///     Inserts or updates <paramref name="characterId" />'s own entry with <paramref name="killTotal" />,
    ///     then re-sorts the whole leaderboard descending by kill total (ties left in whatever relative order
    ///     <see cref="List{T}.Sort()" /> happens to produce -- the source contract's own Edge case "Tie
    ///     ordering is non-deterministic" applies here verbatim, not just as a caveat). Returns
    ///     <see langword="false" /> with no state change when <paramref name="characterId" /> is not already
    ///     tracked and every one of the <see cref="Capacity" /> slots is already occupied by a distinct killer
    ///     -- the 1001st distinct killer this instance's battle history ever sees is silently untracked,
    ///     matching the source contract's own "Leaderboard capacity is 1000 distinct players" edge case
    ///     exactly.
    /// </summary>
    public bool RecordKill(int characterId, string name, byte tribe, int killTotal)
    {
        var index = _entries.FindIndex(e => e.CharacterId == characterId);
        if (index < 0)
        {
            if (_entries.Count >= Capacity)
                return false;

            index = _entries.Count;
            _entries.Add(default);
        }

        _entries[index] = new KillFeedRankedEntry(characterId, name, tribe, killTotal);
        _entries.Sort(static (a, b) => b.Kills.CompareTo(a.Kills));
        return true;
    }

    /// <summary>
    ///     Up to the top 3 entries by kill total, highest first. Fewer than 3 (down to empty) when this
    ///     leaderboard has not yet recorded that many distinct killers -- callers building the outgoing
    ///     kill-feed broadcast are responsible for their own "blank" padding to a fixed count of 3
    ///     (<see cref="KillFeedTopEntry.Blank" />), matching the source contract's own step 1 ("the top-3
    ///     slots of the outgoing feed payload are first unconditionally blanked ... ").
    /// </summary>
    public ImmutableArray<KillFeedRankedEntry> GetTopThree()
    {
        var count = Math.Min(3, _entries.Count);
        return count == 0 ? [] : [.. _entries.Take(count)];
    }

    /// <summary>
    ///     Resets every tracked entry to empty (battle open/reset/close, source contract step 12). Deliberately
    ///     does NOT touch any player's own running kill counter -- see class remarks and
    ///     <see cref="World.PlayerRuntimeState.SessionKillCount" />.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }
}
