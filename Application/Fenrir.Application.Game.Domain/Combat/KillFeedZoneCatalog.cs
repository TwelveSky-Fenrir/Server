using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Per-zone gating for the C15 kill-feed/top-3-ranking mechanic (<see cref="KillFeedLeaderboard" />):
///     which map ids get a persistent leaderboard store allocated at all, and -- a narrower subset -- which
///     of those actually broadcast the kill-feed line to their own connected clients.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1681-1686 (the leaderboard store is allocated once at
///         instance init, only for instances flagged as war zone TYPE 049, 051, 053, 267, or 335) ;
///         Server/ts25zone/S07_MyGame02.cpp:476-485 (only zone types 049, 335, and 267 actually broadcast the
///         kill-feed line -- 038/051/053/194/195 are present in source only as commented-out/disabled
///         conditions and never broadcast).
///     </para>
///     <para>
///         <b>049-type resolution:</b> the source contract names these five entries as "war zone TYPE" ids,
///         not necessarily literal map/server numbers -- the rest of this codebase already establishes that a
///         single "Zone049-type" classification covers a whole family of concrete server numbers (49, 146,
///         149, 154, 157, 160, 120, 121, 122, 295, 164 -- see <see cref="RegularWarMapCatalog" />'s
///         own remarks), not the literal number 49 alone. This catalog reuses
///         <see cref="RegularWarMapCatalog.TryGet" /> as the faithful "is this map Zone049-type"
///         predicate rather than hardcoding the bare number 49, which would silently exclude the other ten
///         configured maps.
///     </para>
///     <para>
///         <b>051-type / 053-type: deliberately NOT modeled.</b> Unlike Zone049-type, no catalog in this
///         codebase enumerates a concrete server-number family for either "Zone051-type" or "Zone053-type",
///         and <see cref="RegularWarActiveMapTracker" />'s own remarks already establish (for a
///         different but related gate, <c>ProcessAttack02</c>'s "close the fight" check) that "the four
///         else-if arms the legacy gate also carries (Zone051/053/194/267) compile but their server-type
///         flags are permanently false" in the shipped build -- i.e. no live map is ever actually flagged as
///         either type. Rather than invent a server-number list nothing in this contract or the existing
///         codebase supplies, these two types are omitted here; since they are also excluded from the
///         narrower feed-enabled set anyway (see the class remarks above), the only observable effect of this
///         omission is that a hypothetical Zone051/053-type map's kills are not tracked at all -- flagged as
///         an open question rather than silently guessed.
///     </para>
///     <para>
///         <b>267:</b> treated as a single literal map number, matching
///         <see cref="PvpKillRewardZoneCatalog" />'s own established precedent (its own
///         <c>UnconditionalFullRewardZoneIds</c> table) of listing 267 as a standalone, reachable zone number
///         (unlike 051/053).
///     </para>
/// </remarks>
public static class KillFeedZoneCatalog
{
    /// <summary>
    ///     Server/Header/Protocol/DEFINE.h:99 (<c>FFAMAPNUM</c>) -- same value as
    ///     <see cref="PvpKillRewardZoneCatalog.FfaMapNumber" />.
    /// </summary>
    public const short FfaMapNumber = PvpKillRewardZoneCatalog.FfaMapNumber;

    /// <summary>The one "Zone267-type" map this catalog can name concretely -- see class remarks.</summary>
    public const short Zone267MapNumber = 267;

    private static readonly FrozenSet<short> ExplicitLeaderboardBackedZoneIds =
        new[] { Zone267MapNumber, FfaMapNumber }.ToFrozenSet();

    /// <summary>
    ///     True when <paramref name="mapId" /> is one of the war-zone types that get a persistent kill-feed
    ///     leaderboard store allocated at all (Zone049-type via <see cref="RegularWarMapCatalog" />,
    ///     plus 267 and the FFA map 335 -- see class remarks for why 051/053-type are omitted).
    /// </summary>
    public static bool HasLeaderboardStore(short mapId)
    {
        return RegularWarMapCatalog.TryGet(mapId, out _) || ExplicitLeaderboardBackedZoneIds.Contains(mapId);
    }

    /// <summary>
    ///     True when a kill in <paramref name="mapId" /> actually broadcasts the kill-feed line to this zone's
    ///     own connected clients. Identical to <see cref="HasLeaderboardStore" /> in this implementation --
    ///     the only two source zone types (051/053-type) that would have narrowed this set are already
    ///     excluded from <see cref="HasLeaderboardStore" /> itself, see class remarks.
    /// </summary>
    public static bool IsFeedEnabled(short mapId)
    {
        return HasLeaderboardStore(mapId);
    }
}
