using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Populates <see cref="TribeGuardCorridorCatalog" /> with the real sixteen-corridor-zone / shared-hub table
///     from <c>MyUtil::WrapCheck</c>'s own destination switch, as enumerated by the A8-corridor-respawn
///     legacy-behavior-translator contract's "Corridor gate -- the four tribe corridors" edge case. Replaces
///     the documented <see cref="TribeGuardCorridorCatalog.Empty" /> placeholder that class's own remarks flag
///     as "a follow-up data-gathering task."
///     <para>
///         Deliberately does NOT populate the per-checkpoint five-guard-post monster-slot table
///         (<see cref="TribeGuardCorridorCatalog.TryGetGuardPostSlots" />): the contract's own Preconditions and
///         Open Question 5 flag the guard-post base index itself (<c>H01_MainApplication.h:79</c>) as
///         corroborated only indirectly, not independently re-opened -- inventing slot numbers here would risk a
///         silently-wrong liveness read, which <see cref="TribeGuardCorridorStateDerivationSystem" />'s own
///         documented "no configured guard-post slots yet -- safe no-op" branch already absorbs without one.
///         Once that base index and its per-segment layout are independently verified, extend this factory (or
///         add a sibling) rather than editing <see cref="TribeGuardCorridorCatalog" /> itself.
///     </para>
/// </summary>
/// <remarks>
///     Réf. (A8-corridor-respawn contract, "Corridor gate -- the four tribe corridors" / "which zone owns
///     (populates) each stone") : Server/ts25zone/S07_MyGame03.cpp:5721-5943 -- for each tribe, one town, three
///     corridor zones, the shared center 38:
///     <list type="bullet">
///         <item>Tribe 0: town 1, corridor 2 / 3 / 4, center 38.</item>
///         <item>Tribe 1: town 6, corridor 7 / 8 / 9, center 38.</item>
///         <item>Tribe 2: town 11, corridor 12 / 13 / 14, center 38.</item>
///         <item>Tribe 3: town 140, corridor 141 / 142 / 143, center 38.</item>
///     </list>
///     The innermost stone (segment 0) of each tribe's own chain is owned by the shared hub (38) and gates entry
///     into that tribe's zone closest to the hub (4 / 9 / 14 / 143 respectively); each further segment is owned
///     by the zone one step further out and gates the next zone toward town, with the outermost segment
///     (segment 3) owned by the zone immediately adjacent to town and gating the town itself -- see
///     <see cref="TribeGuardCorridorCatalog" />'s own class remarks for the general owning-zone rule this
///     table instantiates. <see cref="HubZoneId" /> = 38 is corroborated independently by
///     <c>Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService.SpawnMapIdByTribe</c> = [1, 6, 11,
///     140] (the same four town zone numbers, sourced from the sibling <c>GetReturnBornInTownLocation</c>
///     citation), and the win-zone-038 destination group (Server/ts25zone/S07_MyGame01.cpp:3114-3117) already
///     ported as <see cref="WorldState.WorldStateService.GetAllyOf" />'s own zone-038-winner concept. A THIRD,
///     entirely independent legacy citation also corroborates the twelve non-town corridor zone ids:
///     <see cref="AntiCampingGuardPointCatalog.GuardedMapIds" /> =
///     <c>
///         [2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142,
///         143]
///     </c>
///     (Server/ts25zone/S07_MyGame01.cpp:2074-2371, <c>FIX_HSB_POS_BUG</c>'s own fixed guarded-server
///     list) is the exact same twelve zone ids used here, sourced from an unrelated anti-camping mechanism.
/// </remarks>
public static class TribeGuardCorridorCatalogFactory
{
    /// <summary>The shared center every tribe's own corridor chain terminates at.</summary>
    public const short HubZoneId = 38;

    /// <summary>Tribe 0 (town 1): innermost (hub-adjacent, segment 0) to outermost (segment 3, the town itself).</summary>
    private static readonly ImmutableArray<short> Tribe0Chain = [4, 3, 2, 1];

    /// <summary>Tribe 1 (town 6).</summary>
    private static readonly ImmutableArray<short> Tribe1Chain = [9, 8, 7, 6];

    /// <summary>Tribe 2 (town 11).</summary>
    private static readonly ImmutableArray<short> Tribe2Chain = [14, 13, 12, 11];

    /// <summary>Tribe 3 (town 140).</summary>
    private static readonly ImmutableArray<short> Tribe3Chain = [143, 142, 141, 140];

    /// <summary>
    ///     Builds the live, fully chain-populated catalog. Guard-post monster-slot data is not yet configured --
    ///     see this class's own remarks for why that half stays a documented gap rather than a guess.
    /// </summary>
    public static TribeGuardCorridorCatalog BuildLive()
    {
        var chains = new Dictionary<byte, TribeGuardCorridorChain>
        {
            [0] = new(Tribe0Chain),
            [1] = new(Tribe1Chain),
            [2] = new(Tribe2Chain),
            [3] = new(Tribe3Chain)
        }.ToImmutableDictionary();

        return new TribeGuardCorridorCatalog(
            HubZoneId,
            chains,
            ImmutableDictionary<(byte TribeId, byte SegmentIndex), ImmutableArray<int>>.Empty);
    }
}
