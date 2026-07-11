using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     A11 -- reference table for the tower-war / tournament ("Proving Grounds", EXP-tower, zone-319 war)
///     zone-to-center event-sort codes in the 751-774 band (contract point 6, ts25center/S04_MyWork02.cpp:
///     160-166,854-857,1085-1127). In the shipped legacy build EVERY one of these codes is recognized-but-inert
///     on the hub: each case falls through with no durable world-state mutation, with the associated tribe/stone/
///     zone-war writes present only as commented-out code. Fenrir's two-executable topology has no ts25center
///     process at all, so there is nothing to reproduce -- this is a documentation/landing-point table only, not
///     a behavior, so a future center-side ingestor (out of this workstream's scope) has one place to recognize
///     the band as inert rather than re-deriving it.
///     <para>
///         Whether the hub relays any of these codes onward to clients (versus fully dropping them) is NOT
///         observable within the cited range -- do not assume an onward client relay; flagged for cpp-ts25-explorer
///         re-check if that is ever needed. The three codes in this band that Fenrir DOES actively emit on the
///         ZONE side (752 tower state, 753 attack state, 754 first-hit) are produced by
///         <see cref="ZoneWar.ZoneEventBroadcaster.AnnounceTowerStatus" /> and
///         <c>Zone.Combat.ApplyTowerGuardianHitSideEffects</c> / <see cref="TowerLifecycleSystem" />, not here.
///     </para>
/// </summary>
public static class TowerWarEventCodes
{
    /// <summary>Proving Grounds new-win announcement (contract: 751).</summary>
    public const int ProvingGroundsNewWin = 751;

    /// <summary>
    ///     Tower-state hub broadcast, ZONE-emitted, client-facing (contract: 752). See
    ///     <see cref="ZoneWar.ZoneEventBroadcaster.AnnounceTowerStatus" />.
    /// </summary>
    public const int TowerState = 752;

    /// <summary>
    ///     Tower attack-state hub broadcast, ZONE-emitted (contract: 753); no receiver in Fenrir's topology, logged not
    ///     sent.
    /// </summary>
    public const int TowerAttackState = 753;

    /// <summary>Tower first-hit siege-started hub announcement, ZONE-emitted (contract: 754); logged not sent.</summary>
    public const int TowerFirstHit = 754;

    /// <summary>Tournament countdown (contract: 755).</summary>
    public const int Countdown = 755;

    /// <summary>Zone-319 war lifecycle band open/close/start/draw/victory/return/reset (contract: 756-763).</summary>
    public const int Zone319WarBandStart = 756;

    /// <summary>Zone-319 war lifecycle band end (inclusive) -- see <see cref="Zone319WarBandStart" />.</summary>
    public const int Zone319WarBandEnd = 763;

    /// <summary>Proving Grounds win/lose/remaining/win result band (contract: 771-774).</summary>
    public const int ProvingGroundsResultBandStart = 771;

    /// <summary>Proving Grounds result band end (inclusive) -- see <see cref="ProvingGroundsResultBandStart" />.</summary>
    public const int ProvingGroundsResultBandEnd = 774;

    /// <summary>
    ///     Every event-sort code in the 751-774 band the legacy hub recognized-but-inertly received. Read-only,
    ///     built once -- a future center-side ingestor can test membership here to treat the whole band as a
    ///     no-op without a numeric literal at its own call site.
    /// </summary>
    public static readonly FrozenSet<int> RecognizedInertCodes =
        BuildBand(ProvingGroundsNewWin, ProvingGroundsResultBandEnd);

    /// <summary>True if <paramref name="eventSortCode" /> is one of the recognized-but-inert tower-war codes.</summary>
    public static bool IsRecognizedInert(int eventSortCode)
    {
        return RecognizedInertCodes.Contains(eventSortCode);
    }

    private static FrozenSet<int> BuildBand(int inclusiveStart, int inclusiveEnd)
    {
        var codes = new int[inclusiveEnd - inclusiveStart + 1];
        for (var i = 0; i < codes.Length; i++)
            codes[i] = inclusiveStart + i;

        return codes.ToFrozenSet();
    }
}
