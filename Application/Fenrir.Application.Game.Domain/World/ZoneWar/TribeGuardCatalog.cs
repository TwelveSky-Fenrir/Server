using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     One fixed guard slot position inside a <see cref="GuardPostDefinition" /> -- a world coordinate plus its
///     own stable identity (<see cref="ReservedSlotIndex" />) inside that map's own reserved guard-pool range
///     (<c>START_NORMAL_GUARD_OBJECT_NUM</c> / <c>START_TRIBE_GUARD_OBJECT_NUM</c>,
///     <c>Server/ts25zone/H01_MainApplication.h:79-83</c>, 100 slots each). <see cref="ReservedSlotIndex" />
///     only needs to be unique within its own map's own pool -- <see cref="TribeGuardSpawner" /> keys guard
///     monsters into that map's own <see cref="Zone" /> monster table, so two different maps reusing the same
///     index value never collide.
/// </summary>
public readonly record struct GuardSlotCoordinate(float X, float Y, float Z, int ReservedSlotIndex);

/// <summary>
///     One tribe's (or, for the zone038-winner pool, the winning tribe's) fixed guard post on one map --
///     <c>MySummon::SummonGuard</c>'s per-<c>mServerNumber</c> <c>tGuardSummonCoord[tribe][0..4][xyz]</c> table
///     (<c>Server/ts25zone/S10_MySummon.cpp:1339-1741</c>), exactly <see cref="SlotsPerPost" /> slots per post.
///     <see cref="RequiresTribeSymbolOwnedBy" /> models the conditional 5th post that exists on a tribe's own
///     home-capital map: non-null only for that extra post, and only that tribe's own continued tribe-symbol
///     ownership gates it (Side effects §4 of the <c>SummonGuard</c> contract).
///     <para>
///         The concrete per-map/tribe coordinate and monster main/special-type values are hardcoded in that
///         <c>switch (mServerNumber)</c> and are NOT reproduced here -- this type only models the *shape* the
///         behavior contract describes. Populating a real <see cref="GuardPostCatalog" /> with the actual
///         per-map table is a follow-up data-gathering task against <c>S10_MySummon.cpp:1339-1741</c> (see this
///         cluster's own final report); an empty catalog makes every evaluation a documented no-op, never a
///         guess.
///     </para>
/// </summary>
public sealed record GuardPostDefinition(
    short MapId,
    byte TribeId,
    byte MonsterMainType,
    byte MonsterSpecialType,
    ImmutableArray<GuardSlotCoordinate> Slots,
    byte? RequiresTribeSymbolOwnedBy = null)
{
    /// <summary>Every post has exactly 5 fixed slots (Side effects §2 of the <c>SummonGuard</c> contract).</summary>
    public const int SlotsPerPost = 5;
}

/// <summary>
///     The two independent guard pools <c>SummonGuard(tCheckZone038, ...)</c> switches between -- see
///     <see cref="TribeGuardSpawner" />'s own remarks for the full rule set. <see cref="Empty" /> is the safe
///     default (no posts configured for any map): every evaluation against it is a no-op until the real
///     per-map/tribe table is supplied.
/// </summary>
public sealed class GuardPostCatalog(
    ImmutableArray<GuardPostDefinition> ordinaryPosts,
    ImmutableArray<GuardPostDefinition> zone038WinnerPosts)
{
    public static readonly GuardPostCatalog Empty = new([], []);

    /// <summary><c>tCheckZone038 == false</c>: one post per tribe, on whichever of the 13 eligible maps hosts it.</summary>
    public ImmutableArray<GuardPostDefinition> OrdinaryPosts { get; } = ordinaryPosts;

    /// <summary><c>tCheckZone038 == true</c>: one post per tribe, all on <see cref="TribeGuardSpawner.Zone038MapId" />.</summary>
    public ImmutableArray<GuardPostDefinition> Zone038WinnerPosts { get; } = zone038WinnerPosts;
}
