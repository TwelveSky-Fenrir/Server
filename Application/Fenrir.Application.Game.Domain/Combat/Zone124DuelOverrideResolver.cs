namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>Result of the zone124 final-countdown duel override (<see cref="Zone124DuelOverrideResolver.Apply" />).</summary>
/// <param name="Damage">The (possibly tripled) outgoing duel damage to carry into the divide-by-five.</param>
/// <param name="CritExists">The crit-exists display flag -- forced true while the override is active.</param>
public readonly record struct Zone124OverrideOutcome(int Damage, bool CritExists);

/// <summary>
///     Branch C of the B10 contract: on a map-124 process, a DUEL-CLASS hit whose zone-wide mass-duel countdown
///     is below ten remaining units has its (already ordinary-crit-doubled) damage tripled and its crit-exists
///     display flag forced on, with no probability check -- applied AFTER the ordinary duel critical doubling
///     and BEFORE the duel path's fixed divide-by-five
///     (<c>Server/ts25zone/S07_MyGame02.cpp:1146-1150, :1157</c>). Enemy-class hits and non-124 processes never
///     reach this. Pure: the caller supplies the map-124 flag and the live countdown value (from
///     <see cref="Zone124MassDuelState.RemainingUnits" />).
/// </summary>
/// <remarks>
///     Illustration from the contract: a base hit of 100 -&gt; 200 (ordinary crit) -&gt; 600 (this triple) -&gt;
///     120 (the duel divide-by-five). The triple stacks multiplicatively on the ordinary doubling. The gate is
///     the literal "countdown strictly below ten"; in practice a map-124 duel hit only reaches this at all while
///     a mass-duel event is running (the two combatants must share the event's duel-pairing markers), so a
///     cleared countdown of 0 never actually reaches the override -- the enrollment gate upstream, not this
///     predicate, is what enforces "an event is running."
/// </remarks>
public static class Zone124DuelOverrideResolver
{
    /// <summary>
    ///     Fenrir's one-map-per-Zone id for the legacy map-124 duel-arena server type
    ///     (<c>H07_MyGame.h:19,238-242</c>). Matches <c>Zone.Combat.DamagePipeline.cs</c>'s own
    ///     <c>ReflectDisabledZoneId</c> constant (reflect is likewise disabled on map 124).
    /// </summary>
    public const short Zone124MapId = 124;

    /// <summary>The override fires while the countdown is strictly below this many units (:1146-1150).</summary>
    public const int FinalCountdownThreshold = 10;

    /// <summary>The unconditional damage multiplier applied in the final countdown (:1146-1150).</summary>
    public const int DamageMultiplier = 3;

    /// <summary>Whether the final-countdown override is active for this duel hit.</summary>
    public static bool IsActive(bool isMap124Process, int countdownRemaining)
    {
        return isMap124Process && countdownRemaining < FinalCountdownThreshold;
    }

    /// <summary>
    ///     Applies the override to a duel hit's post-crit-doubling damage and crit-exists flag. When inactive,
    ///     returns the inputs unchanged. Call between the ordinary crit doubling and the divide-by-five.
    /// </summary>
    public static Zone124OverrideOutcome Apply(int damage, bool critExists, bool isMap124Process,
        int countdownRemaining)
    {
        return IsActive(isMap124Process, countdownRemaining)
            ? new Zone124OverrideOutcome(damage * DamageMultiplier, true)
            : new Zone124OverrideOutcome(damage, critExists);
    }
}
