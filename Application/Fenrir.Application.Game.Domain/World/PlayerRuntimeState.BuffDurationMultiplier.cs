using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Session-cached inputs/output for <c>mSupportSkillTimeUpRatio</c> (behavior contract
///     "buff-application-stacking-decay", <c>MyUser::SetUserBonus2</c>, Server/ts25zone/S03_MyUser.cpp:524-541)
///     -- the self-buff skill duration multiplier <see cref="Skills.SkillCastResolver.TryCast" /> reads at
///     cast time via <see cref="SupportSkillTimeUpRatio" />. Recomputed (never read-through) at the trigger
///     points the contract identifies: zone-enter/character-load (<see cref="Zone.HandleEnter" />) and the
///     once-per-real-minute expiry check
///     (<see cref="Simulation.SupportSkillTimeUpRatioMaintenanceSystem" />). Mutated only from this
///     character's own zone tick thread, same single-writer contract as every other field on this type.
/// </summary>
public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy <c>aPremium</c> -- Unix epoch seconds the character's Premium status expires at, 0 = none.
    ///     Sourced from <c>game.Characters.PremiumExpireUtc</c> at world entry
    ///     (<c>EnterWorldService.HandleAsync</c>) and carried through an in-process zone transfer
    ///     (<see cref="ZoneTransfer.CreateEnterData" />) -- NOT re-read from SQL on a same-shard hop, same
    ///     "carry the live in-memory value, don't re-query" posture as <see cref="HeroRankPoints" />.
    /// </summary>
    public long PremiumExpireUtc { get; set; }

    /// <summary>
    ///     Legacy <c>aBuffX2Time</c> -- the Buff-Duration-Extension consumable's remaining whole-number
    ///     countdown, positive = active, 0 = inactive. Session-only: no <c>game.Characters</c> column exists
    ///     for this field yet (same "not yet persisted" posture as <see cref="PlayTime1" />), and no item-
    ///     consumption path in Fenrir currently increases it (behavior contract's own recompute-trigger 3 --
    ///     the "Buff Duration Pill", item 1132 -- is explicitly out of scope here: its own grant amount/value
    ///     semantics are not established by any citation available to this pass). Always 0 today; the field
    ///     exists so <see cref="RecomputeSupportSkillTimeUpRatio" /> and
    ///     <see cref="Simulation.SupportSkillTimeUpRatioMaintenanceSystem" /> are ready for that follow-up
    ///     without another PlayerRuntimeState-shape change.
    /// </summary>
    public int BuffX2Time { get; set; }

    /// <summary>
    ///     Cached <c>mSupportSkillTimeUpRatio</c> itself (1, 2, or 4) -- the only field
    ///     <see cref="Skills.SkillCastResolver.TryCast" /> ever reads; defaults to 1 (neutral) so a
    ///     <see cref="PlayerRuntimeState" /> constructed without going through
    ///     <see cref="RecomputeSupportSkillTimeUpRatio" /> at least once (e.g. a unit test) never silently
    ///     multiplies a duration by an uninitialized value.
    /// </summary>
    public int SupportSkillTimeUpRatio { get; set; } = 1;

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.SupportSkillTimeUpRatioMaintenanceSystem" />'s own
    ///     once-per-real-minute cadence -- deliberately a separate counter from
    ///     <see cref="PlayTimeAccrualTicks" /> even though both use the same 120-legacy-tick (60s) period,
    ///     since <see cref="Simulation.PlayTimeAccrualSystem" /> already owns resetting that one every minute
    ///     for its own unrelated PlayTime1-3/Event family -- sharing it here would double-consume/desync both
    ///     systems' cadences.
    /// </summary>
    public int SupportSkillTimeUpRatioAccrualTicks { get; set; }

    /// <summary>
    ///     Recomputes and caches <see cref="SupportSkillTimeUpRatio" /> from this character's current
    ///     <see cref="BuffX2Time" />/<see cref="PremiumExpireUtc" /> -- call at every recompute trigger the
    ///     behavior contract identifies (zone-enter, and the per-minute expiry check once either source field
    ///     is found to have just lapsed), never at cast time itself. <paramref name="nowUnixSeconds" /> is
    ///     taken as a parameter (not read from the clock internally) to keep this pure/unit-testable, matching
    ///     <see cref="Social.Trade.TradeItemPlacementResolver.IsPremiumPageAccessAllowed" />'s own convention
    ///     for the "still unexpired" comparison (&gt;=, not &gt;).
    /// </summary>
    public void RecomputeSupportSkillTimeUpRatio(long nowUnixSeconds)
    {
        var buffDurationExtensionActive = BuffX2Time > 0;
        var premiumActive = PremiumExpireUtc >= nowUnixSeconds;
        SupportSkillTimeUpRatio =
            SupportSkillTimeUpRatioCalculator.Compute(buffDurationExtensionActive, premiumActive);
    }
}
