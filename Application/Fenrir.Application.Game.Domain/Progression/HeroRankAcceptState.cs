namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     The legacy hero-rank per-slot accept marker (<c>hAccept</c>) tri-state, plus the pure transition rules
///     over it. This models the legacy ground truth so the reconciliation with Fenrir's own collapsed model is
///     explicit rather than buried: legacy tracks THREE states per previous-period hero-rank slot, whereas
///     Fenrir persists a single <c>RewardClaimed</c> boolean (<c>HeroRankingRowDto.RewardClaimed</c>).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:14183-14265 (the zone claim promotes an unclaimed slot 0-&gt;1
///     and grants CP; an already-accepted slot is refused with response code 3, no grant).
///     Server/ts25center/S08_MyDB.cpp:449-466 (<c>UpdateRank</c>'s settlement branch promotes every loaded slot
///     with a positive character id and accept-state 1 to accept-state 2, stamping <c>hDate</c> with the current
///     server date-time, and only advances the in-memory state on a successful DB update -- so a failed update
///     leaves the slot at 1 to retry on a later pass; the promotion is idempotent and self-healing).
///     <para>
///         <b>Reconciliation of the C19 "hDate collapsed to a bit" ambiguity flag.</b> The finding's phrasing is
///         not supported by the legacy code, which writes <c>hDate</c> as a full timestamp at the 1-&gt;2
///         promotion. In Fenrir's model the entire 1 vs 2 distinction is a center-server bookkeeping detail with
///         no client-observable effect (the claim itself is the only thing a player sees), so Fenrir collapses
///         both claimed states onto <c>RewardClaimed = true</c> and does NOT run a separate settlement pass: the
///         zone claim writes the durable claimed flag directly and atomically, which is what the legacy's later
///         1-&gt;2 promotion would eventually have persisted anyway. This enum therefore exists to (a) make the
///         claim-eligibility rule read as intent rather than a magic boolean and (b) provide the exact promotion
///         rule should a future feature ever reintroduce the two-phase center settlement; it is not itself a
///         behavioural change to the shipped bool-based flow.
///     </para>
/// </remarks>
public enum HeroRankAcceptState : byte
{
    /// <summary>hAccept 0 -- the slot's reward has not been claimed. The only claimable state.</summary>
    Unclaimed = 0,

    /// <summary>
    ///     hAccept 1 -- the reward has been claimed by the zone but the center has not yet settled it. In
    ///     Fenrir this maps to <c>RewardClaimed = true</c> (see <see cref="HeroRankAcceptStateRules" />).
    /// </summary>
    ClaimedPendingSettlement = 1,

    /// <summary>hAccept 2 -- claimed and settled by the center's <c>UpdateRank</c> pass. Also claimed = true.</summary>
    Settled = 2
}

/// <summary>Pure transition/interpretation rules over <see cref="HeroRankAcceptState" />.</summary>
public static class HeroRankAcceptStateRules
{
    /// <summary>
    ///     A slot's reward may be claimed only from <see cref="HeroRankAcceptState.Unclaimed" />. Both claimed
    ///     states refuse a re-claim (the legacy's response code 3, "already received").
    /// </summary>
    public static bool IsClaimable(HeroRankAcceptState state)
    {
        return state == HeroRankAcceptState.Unclaimed;
    }

    /// <summary>True once the reward has been claimed, regardless of whether the center has settled it yet.</summary>
    public static bool IsClaimed(HeroRankAcceptState state)
    {
        return state is HeroRankAcceptState.ClaimedPendingSettlement or HeroRankAcceptState.Settled;
    }

    /// <summary>
    ///     The center settlement transition: only a <see cref="HeroRankAcceptState.ClaimedPendingSettlement" />
    ///     slot advances to <see cref="HeroRankAcceptState.Settled" />; an unclaimed or already-settled slot is
    ///     returned unchanged, so the pass is idempotent and safe to retry.
    /// </summary>
    public static HeroRankAcceptState PromoteToSettled(HeroRankAcceptState state)
    {
        return state == HeroRankAcceptState.ClaimedPendingSettlement ? HeroRankAcceptState.Settled : state;
    }

    /// <summary>
    ///     Maps Fenrir's collapsed <c>RewardClaimed</c> flag onto the tri-state. A null or false flag is
    ///     <see cref="HeroRankAcceptState.Unclaimed" />; true is <see cref="HeroRankAcceptState.Settled" />
    ///     (Fenrir persists the terminal claimed state directly, having no separate pending phase).
    /// </summary>
    public static HeroRankAcceptState FromClaimedFlag(bool? rewardClaimed)
    {
        return rewardClaimed == true ? HeroRankAcceptState.Settled : HeroRankAcceptState.Unclaimed;
    }

    /// <summary>The inverse of <see cref="FromClaimedFlag" />: any claimed state is <c>RewardClaimed = true</c>.</summary>
    public static bool ToClaimedFlag(HeroRankAcceptState state)
    {
        return IsClaimed(state);
    }
}
