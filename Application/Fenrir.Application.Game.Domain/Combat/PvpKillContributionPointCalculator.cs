namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Pure CP-formula and CP-cap-clamp helpers for the PvP-kill reward pipeline
///     (<c>MyUtil::ProcessForKillOtherTribe</c>, S07_MyGame03.cpp:2602-3248) and its two dedicated flat-amount
///     overrides (<c>RegularWar_ProcessKillReward</c>/<c>FFA_ProcessKillReward</c>, S07_MyGame03.cpp:2402-2504).
///     No I/O, no state -- <see cref="Fenrir.Application.Game.Domain.World.Zone" /> is the only caller and owns
///     every input (buffs, world-state, tower control) this needs.
/// </summary>
/// <remarks>
///     <para>
///         <b>Terms deliberately NOT computed here, and why:</b> the source contract states three of the
///         generic formula's additive bonuses only as "one specific server number/tribe combination" (+2), "one
///         specific server/level/world-flag combination" (+10), and "four specific zone numbers when the
///         attacker's tribe is not the owning tribe of that zone" (+10) -- the exact conditions (which
///         server/tribe/level/zone) are not given by that contract and recovering them requires reading
///         <c>Server/ts25zone/S07_MyGame03.cpp</c> directly, which is out of this agent's scope. Only the two
///         bonuses whose trigger condition IS fully specified (premium status, warrior-scroll buff) are
///         computed by <see cref="ComputeBaseAmount" />; the other three are omitted rather than guessed.
///         Similarly, the "per-character override," "per-tribe world-state bonus," and "tower-control bonus"
///         terms are accepted as caller-supplied parameters (defaulting to 0) rather than hardcoded, since
///         this contract does not say how large they are or exactly which WorldState/TowerWarState field
///         feeds them.
///     </para>
///     <para>
///         <b>
///             The two dedicated flat-amount overrides are a SEPARATE, fully-cited contract from the generic
///             formula above:
///         </b>
///         <see cref="RegularWarOverrideFlatCpAmount" />/<see cref="FfaOverrideFlatAmount" />
///         (both +20 CP), <see cref="RegularWarOverrideWarPointAmount" /> (+2 War Point), and
///         <see cref="RegularWarOverrideBloodPointAmount" /> (+2 Blood/DS Point) were opened and verified
///         directly against <c>Server/ts25zone/S07_MyGame03.cpp:2402-2504</c> -- unlike <see cref="BasePerKillAmount" />
///         and <see cref="PlaceholderHardCap" /> above, these four are real game-balance values, not placeholders.
///     </para>
/// </remarks>
public static class PvpKillContributionPointCalculator
{
    /// <summary>
    ///     "Starts from a configured base value" -- no legacy source value plumbed to this contract.
    ///     Placeholder, not real game-balance tuning.
    /// </summary>
    public const int BasePerKillAmount = 2;

    /// <summary>"+2 if the attacker has an active premium status" -- literal bonus given by the source contract.</summary>
    public const int PremiumStatusBonus = 2;

    /// <summary>"+1 if the attacker holds a 'warrior scroll' buff" -- literal bonus given by the source contract.</summary>
    public const int WarriorScrollBuffBonus = 1;

    /// <summary>
    ///     "A fixed hard cap" on total contribution points -- no legacy source value plumbed to this contract.
    ///     Placeholder ceiling so <see cref="ClampGrant" /> has something concrete to clamp against in
    ///     production; callers that know the real cap should pass their own value instead of this one.
    /// </summary>
    public const int PlaceholderHardCap = 2_000_000_000;

    /// <summary>
    ///     <c>FFA_ProcessKillReward</c>'s flat CP grant (S07_MyGame03.cpp:2499, via the same <c>getMaxCP</c>
    ///     cap-clamp helper as <see cref="RegularWarOverrideFlatCpAmount" />) -- confirmed +20, identical to the
    ///     Regular-War-host override despite the two being separate source functions with their own separate
    ///     cooldown tables (see <see cref="FlatOverrideCooldown" />).
    /// </summary>
    public const int FfaOverrideFlatAmount = 20;

    /// <summary>
    ///     <c>RegularWar_ProcessKillReward</c>'s flat CP grant (S07_MyGame03.cpp:2446, via the <c>getMaxCP</c>
    ///     cap-clamp helper, see <see cref="ClampGrant" />) -- confirmed +20. Kept as its own named constant
    ///     rather than reusing <see cref="FfaOverrideFlatAmount" /> or <see cref="BasePerKillAmount" />: the two
    ///     overrides are textually distinct legacy functions that only happen to grant the same amount, and
    ///     <see cref="BasePerKillAmount" /> already has its own, unrelated citation (the generic formula,
    ///     S07_MyGame03.cpp:2602-3248).
    /// </summary>
    public const int RegularWarOverrideFlatCpAmount = 20;

    /// <summary>
    ///     <c>RegularWar_AddWarPoint</c>'s flat War Point grant (S07_MyGame03.cpp:2453) -- confirmed +2, granted
    ///     unconditionally alongside <see cref="RegularWarOverrideFlatCpAmount" /> whenever the Regular-War-host
    ///     override fires. FFA-335's override grants no War Point at all (S07_MyGame03.cpp:2458-2504 has no
    ///     equivalent call).
    /// </summary>
    public const int RegularWarOverrideWarPointAmount = 2;

    /// <summary>
    ///     Flat Blood Point/DS Point grant (S07_MyGame03.cpp:2456) -- confirmed +2, granted unconditionally
    ///     alongside <see cref="RegularWarOverrideFlatCpAmount" /> whenever the Regular-War-host override fires.
    ///     FFA-335's override grants no Blood Point at all (same absence as <see cref="RegularWarOverrideWarPointAmount" />).
    /// </summary>
    public const int RegularWarOverrideBloodPointAmount = 2;

    /// <summary>
    ///     "Its own independent 2-minute same-victim-pair cooldown" -- shared literal for both the
    ///     Regular-War-host and FFA-335 CP overrides (step 4/5 of the source contract), each using its own
    ///     separate cooldown-state table (a fresh <see cref="KillCooldownTracker" /> instance per override,
    ///     not the main C05 anti-farm tracker).
    /// </summary>
    public static readonly TimeSpan FlatOverrideCooldown = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Base grant amount before the CP-cap clamp: <paramref name="basePerKillAmount" /> (defaults to
    ///     <see cref="BasePerKillAmount" />) plus <paramref name="perCharacterOverride" /> plus
    ///     <paramref name="perTribeWorldStateBonus" /> plus <paramref name="towerControlBonus" />
    ///     (all default 0 -- see class remarks for why none of the three is computed from real state yet),
    ///     plus <see cref="PremiumStatusBonus" />/<see cref="WarriorScrollBuffBonus" /> when the corresponding
    ///     flag is set.
    /// </summary>
    public static int ComputeBaseAmount(
        bool hasPremiumStatus,
        bool hasWarriorScrollBuff,
        int perCharacterOverride = 0,
        int perTribeWorldStateBonus = 0,
        int towerControlBonus = 0,
        int basePerKillAmount = BasePerKillAmount)
    {
        var amount = basePerKillAmount + perCharacterOverride + perTribeWorldStateBonus + towerControlBonus;

        if (hasPremiumStatus)
            amount += PremiumStatusBonus;
        if (hasWarriorScrollBuff)
            amount += WarriorScrollBuffBonus;

        return amount;
    }

    /// <summary>
    ///     <c>getMaxCP</c> (S07_MyGame03.cpp:2363-2371) semantics: NOT a simple add. Returns the amount that
    ///     can actually be granted without pushing <paramref name="currentTotal" /> past <paramref name="cap" />
    ///     -- reduced, or in principle negative, whenever the full <paramref name="amountToAdd" /> would
    ///     overshoot. Callers must add the RETURNED value to the running total, never the original
    ///     <paramref name="amountToAdd" />.
    /// </summary>
    public static int ClampGrant(int currentTotal, int amountToAdd, int cap)
    {
        return Math.Min(amountToAdd, cap - currentTotal);
    }
}
