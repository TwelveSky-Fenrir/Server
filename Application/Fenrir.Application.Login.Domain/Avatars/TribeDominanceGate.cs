namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2's dominant-tribe creation gate -- blocks creation into whichever tribe
///     currently holds the single highest point total, but only once that tribe's own total has reached 100.
///     This is an absolute floor on the leader's own total, never a margin/lead comparison against the other
///     tribes: an older sum-based margin algorithm exists in the legacy source, but only as commented-out,
///     never-compiled code immediately above the live check.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:724-730 (the live LNW33 gate: leader lookup, &gt;=100 floor,
///     equality against the requested tribe) ; Server/ts25login/S04_MyWork02.cpp:664-722 (the superseded
///     margin-based algorithm, dead code, intentionally not reproduced here) ; Server/Header/function.h:3124-3144
///     (ReturnBigTribe -- linear scan of all four tribe slots, replacing the running leader only on a
///     strictly-greater total, so ties resolve toward the lowest-numbered tribe) ; Server/Header/Protocol/
///     DEFINE.h:309 (four tribe slots total, including the fourth slot, which <see cref="FourthFactionGate" />
///     excludes as a creation target upstream of this gate whenever
///     <see cref="LoginServerOptions.EnableFourthFaction" /> is in its default disabled state -- in that state
///     <c>requestedTribe</c> can never be 3 here, making this gate a no-op whenever that slot holds the
///     highest total; if the toggle is ever operator-enabled, tribe 3 can reach this gate like any other
///     tribe) ; ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:250-264 (corroborating
///     narrative; its "point gap" phrasing does not match what the cited source actually computes -- the
///     source, not the doc's prose, is authoritative for that discrepancy).
/// </remarks>
public static class TribeDominanceGate
{
    /// <summary>The leader's own point total must reach this before the gate can ever block anything.</summary>
    public const int DominantTribeFloor = 100;

    /// <summary>
    ///     Server/Header/Protocol/DEFINE.h:309 -- four tribe slots total. Kept <c>public</c> rather than
    ///     <c>private</c> so <see cref="FourthFactionGate.FourthFactionTribe" /> (and this constant's own
    ///     tests) can reference this same constant instead of repeating the "4" as a second, independent
    ///     literal that could silently drift out of lockstep with this one.
    /// </summary>
    public const int TribeSlotCount = 4;

    /// <summary>
    ///     True if <paramref name="requestedTribe" /> is the single tribe currently holding the highest point
    ///     total (ties broken toward the lowest-numbered tribe -- an equal-highest tribe is never itself
    ///     treated as "the" leader) and that total has reached <see cref="DominantTribeFloor" />. A tribe slot
    ///     absent from <paramref name="standings" /> is treated as holding zero points, matching the "no
    ///     standings yet" edge case (nothing above the floor, so the gate never engages).
    /// </summary>
    public static bool BlocksCreation(byte requestedTribe, IReadOnlyList<TribeSummaryDto> standings)
    {
        var pointsByTribe = new int[TribeSlotCount];

        foreach (var standing in standings)
            if (standing.TribeId < TribeSlotCount)
                pointsByTribe[standing.TribeId] = standing.Points.GetValueOrDefault();

        var leadingTribe = 0;
        for (var tribeId = 1; tribeId < TribeSlotCount; tribeId++)
            if (pointsByTribe[tribeId] > pointsByTribe[leadingTribe])
                leadingTribe = tribeId;

        return pointsByTribe[leadingTribe] >= DominantTribeFloor && leadingTribe == requestedTribe;
    }
}
