namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     What a zone-move request's corridor check decided. <see cref="Allowed" /> also covers every destination
///     this table doesn't gate at all (the hub, or any non-corridor map). The two rejection outcomes differ
///     only in whether the involved zones also trip the unrelated zone-37 hard-disconnect rule -- see
///     <see cref="TribeGuardCorridorGate" />'s own remarks.
/// </summary>
public enum TribeGuardCorridorMoveOutcome
{
    Allowed,
    RejectedSoft,
    RejectedHardDisconnect
}

/// <summary>
///     <c>MyUtil::WrapCheck</c> (<c>Server/ts25zone/S07_MyGame03.cpp:5721-5943</c>): decides whether a
///     zone-to-zone move into one of the sixteen tribe-corridor zones may proceed. Pure decision logic, same
///     "already-resolved values in, enum out" shape as <see cref="HolyStoneTribeMatch" />/
///     <see cref="Progression.TowerFriendlyFireGate" /> -- no I/O, no session/account concerns beyond the
///     already-resolved GM-rank bool the caller passes in.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:5721-5943 (<c>WrapCheck</c> -- full corridor switch for all
///     four tribes, cases 1-4/6-9/11-14/140-143 ; owner-tribe/ally bypass condition ; single-step adjacency
///     requirement ; directional advance-only gating ; default <c>return TRUE</c> for every non-corridor
///     destination) ; Server/Header/function.h:2964-2975 (<c>ReturnAlliance</c>, resolved against the
///     checkpoint's OWNING tribe, never the requester's own -- same calling convention as
///     <see cref="HolyStoneTribeMatch" />/<see cref="Progression.TowerFriendlyFireGate" />, both already citing
///     this exact "never resolve the ally of the asking party's own tribe" class of legacy bug) ;
///     Server/ts25zone/S04_MyWork02.cpp:2017-2186 (<c>DEMAND_ZONE_SERVER_INFO_2</c> -- destination
///     validity/online preconditions and the GM-rank bypass are the CALLER's concern, not this gate's; the
///     hard-disconnect-vs-soft-failure split at :2148-2166) ; Server/ts25zone/S05_MyTransfer.cpp:1166-1179
///     (<c>B_RETURN_TO_AUTO_ZONE</c> -- the soft-failure fallback-redirect instruction is the CALLER's concern,
///     not this gate's; it carries no destination payload of its own).
///     <para>
///         RESOLVED (previously flagged as an ambiguity here): a re-verified legacy-behavior-translator
///         contract confirms every segment 0-3, including both chain ends, follows the identical rule with no
///         special-case divergence -- retreat toward the hub is always unconditionally allowed, and an advance
///         requires both exact single-step adjacency (the hub for segment 0, the previous segment's own zone
///         otherwise) AND an open guard state, with no exception for the outermost or innermost position. The
///         implementation below already matches this (no segment-specific branching), so this remark now only
///         records that the ambiguity previously noted here was checked and is not real, rather than describing
///         an unresolved open question.
///     </para>
/// </remarks>
public static class TribeGuardCorridorGate
{
    /// <summary>
    ///     Server/ts25zone/S04_MyWork02.cpp:2152-2156 -- the one unrelated reserved zone number that upgrades a
    ///     corridor rejection into an outright disconnect instead of a soft failure response.
    /// </summary>
    public const short HardDisconnectZoneId = 37;

    /// <param name="catalog">The corridor shape -- which zone is which tribe's segment, and chain adjacency.</param>
    /// <param name="state">The live open/closed table this call reads (never mutates).</param>
    /// <param name="requesterTribe">
    ///     The moving character's own tribe. Any value outside 0-3 (including a "not yet assigned" sentinel) can
    ///     never equal a real <paramref name="catalog" /> owning-tribe id or ally id, so it naturally never
    ///     bypasses anything -- no separate nullable marker is needed.
    /// </param>
    /// <param name="originZoneId">The zone the mover's connection is currently hosted on.</param>
    /// <param name="destinationZoneId">The zone number being requested.</param>
    /// <param name="requesterIsGmOrAdminRank">
    ///     True for any account rank above ordinary-player -- the corridor check is skipped entirely
    ///     (S04_MyWork02.cpp:2148), independent of every other input.
    /// </param>
    /// <param name="resolveAllyOfOwnerTribe">
    ///     <see cref="WorldState.WorldStateService.GetAllyOf" /> (or an equivalent), called ONLY with the
    ///     checkpoint's owning tribe id once that is known -- never with <paramref name="requesterTribe" />, per
    ///     this class's own remarks. Null (no alliance data wired) conservatively resolves to "no ally".
    /// </param>
    public static TribeGuardCorridorMoveOutcome Evaluate(
        TribeGuardCorridorCatalog catalog,
        TribeGuardCorridorState state,
        byte requesterTribe,
        short originZoneId,
        short destinationZoneId,
        bool requesterIsGmOrAdminRank,
        Func<byte, byte?>? resolveAllyOfOwnerTribe = null)
    {
        if (requesterIsGmOrAdminRank)
            return TribeGuardCorridorMoveOutcome.Allowed;

        if (!catalog.TryGetSegmentForDestinationZone(destinationZoneId, out var ownerTribeId, out var segmentIndex))
            return TribeGuardCorridorMoveOutcome.Allowed; // not one of the sixteen corridor-zone destinations

        if (requesterTribe == ownerTribeId)
            return TribeGuardCorridorMoveOutcome.Allowed; // the owning tribe itself -- unconditional bypass

        if (resolveAllyOfOwnerTribe?.Invoke(ownerTribeId) is { } allyOfOwner && requesterTribe == allyOfOwner)
            return TribeGuardCorridorMoveOutcome.Allowed; // the owning tribe's declared ally -- unconditional bypass

        var originSegmentIndex = catalog.GetOriginSegmentIndex(ownerTribeId, originZoneId);

        // Retreating toward the hub (origin strictly more interior than destination, same tribe's own chain)
        // is never gated by this mechanism at all, regardless of guard state or adjacency.
        if (originSegmentIndex is { } deeperOrigin && deeperOrigin > segmentIndex)
            return TribeGuardCorridorMoveOutcome.Allowed;

        // Advancing: only a valid iff the mover comes from the ONE zone immediately outside this checkpoint
        // (hub for segment 0, the previous segment's own zone otherwise) -- checked independently of, and in
        // addition to, the guard-state requirement below (no skipping/warping past a checkpoint).
        var isValidSingleStepAdvance = originSegmentIndex == segmentIndex - 1;
        if (!isValidSingleStepAdvance)
            return HardOrSoft(originZoneId, destinationZoneId);

        return state.IsOpen(ownerTribeId, segmentIndex)
            ? TribeGuardCorridorMoveOutcome.Allowed
            : HardOrSoft(originZoneId, destinationZoneId);
    }

    private static TribeGuardCorridorMoveOutcome HardOrSoft(short originZoneId, short destinationZoneId)
    {
        return originZoneId == HardDisconnectZoneId || destinationZoneId == HardDisconnectZoneId
            ? TribeGuardCorridorMoveOutcome.RejectedHardDisconnect
            : TribeGuardCorridorMoveOutcome.RejectedSoft;
    }
}
