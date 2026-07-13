using System.Collections.Frozen;

namespace Fenrir.Cluster.EventBus;

/// <summary>
///     The exhaustive, frozen allowlist of op33 <c>tSort</c> values that are LIVE cases in the legacy
///     <c>W_ZONE_BROADCAST_FOR_CENTER_SEND</c> dispatch under ReleaseEU33 (the value-by-value catalogue in
///     <c>contract_op33-tsort-catalog.md</c>, mined from <c>Server/ts25center/S04_MyWork02.cpp:212-1213</c>).
///     <para>
///         HARDENING — the single most important behavior of this unit: legacy's <c>switch(tSort)</c> has NO
///         <c>default</c>, so any unrecognized <c>tSort</c> fell straight through to the fan-out and was rebroadcast
///         verbatim to the whole cluster (an amplification/injection vector). Fenrir replaces that with a closed
///         allowlist: a <c>tSort</c> NOT in this set is DROPped (no state mutation, no fan-out) and audited under
///         <c>EventLogCategory.AntiCheat</c>. Never reproduce the blind rebroadcast.
///     </para>
///     <para>
///         Membership here means "recognized as a case" — it does NOT distinguish effect-bearing from inert
///         (fan-out-only) <c>tSort</c>; that resolution lives in <see cref="CenterWorldEventIngestor" />'s routing.
///         The two exception sorts <c>9998</c> (hero-rank) and <c>10000</c> (close-proxy) are intentionally ABSENT:
///         they have dedicated, non-fan-out paths handled before this allowlist gate is consulted.
///     </para>
/// </summary>
public static class KnownTSortRegistry
{
    /// <summary>The frozen recognized-<c>tSort</c> allowlist (fan-out gate). See the type remarks.</summary>
    public static readonly FrozenSet<int> KnownSorts = Build();

    /// <summary><see langword="true" /> iff <paramref name="sort" /> is a recognized op33 case eligible for fan-out.</summary>
    public static bool IsKnown(int sort)
    {
        return KnownSorts.Contains(sort);
    }

    private static FrozenSet<int> Build()
    {
        var set = new HashSet<int>();

        // 1..115 are ALL recognized with no gaps: Zone049(1-9), Zone051(10-18), Zone053(19-30),
        // tribe-guardians(31-32), Zone038/symbols(33-45), alliances(46-49), tribe-vote(50-61), 62-63,
        // Zone175(64-100,110), and 101-115 (inert/no-op cases incl. guild-buff 112/113/114/115, 200-window edge).
        AddRange(set, 1, 115);

        // 200 (misc), Zone194(201-208).
        AddRange(set, 200, 208);

        // Zone194 bonus ratios (301), tribe master-call ability (302).
        set.Add(301);
        set.Add(302);

        // Zone267(402-410), Zone241 "Den of Rebirth"(411-415).
        AddRange(set, 402, 415);

        // Fortress/FoL & misc messages. NOTE: 417 is NOT a recognized case in the value-by-value catalogue
        // (the summary line 158 of the contract lists "416-428" but the mined switch, contract line 94, skips 417).
        set.Add(416);
        AddRange(set, 418, 428);
        set.Add(500);

        // Valley-of-Deceased / sieges — explicit set (all inert/fan-out-only; the mZone200TypeState writes are
        // commented out in prod). 659/660/662 are COMMENTED labels and 670/673 are absent from the mined switch,
        // so they are deliberately NOT recognized (the contract's "663-669"/"671-675" summary is looser than the
        // value-by-value list at line 96, which omits 673 — followed here).
        AddRange(set, 601, 615);
        set.Add(621);
        set.Add(628);
        set.Add(661);
        AddRange(set, 663, 669);
        set.Add(671);
        set.Add(672);
        set.Add(674);
        set.Add(675);

        // Tournament / Proving Grounds — all inert. 752 and 764-770 are absent from the mined switch.
        set.Add(751);
        AddRange(set, 753, 763);
        AddRange(set, 771, 774);

        // Guild-buff no-op with a distinct high value (recognized-but-empty body under USE_GUILD_BUFF).
        set.Add(1133);

        // FFA/Zone335 scalar (1501-1507); DTM effect (1510); 1511-1514 inert; 1520 inert.
        AddRange(set, 1501, 1507);
        AddRange(set, 1510, 1514);
        set.Add(1520);

        // FFA daily-refresh trigger: double output (emits 1601 zeroed, then fans out 4000).
        set.Add(4000);

        return set.ToFrozenSet();
    }

    private static void AddRange(HashSet<int> set, int inclusiveLow, int inclusiveHigh)
    {
        for (var value = inclusiveLow; value <= inclusiveHigh; value++)
            set.Add(value);
    }
}
