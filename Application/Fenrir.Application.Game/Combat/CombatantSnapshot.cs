using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Everything <see cref="CombatResolver" /> needs from ONE side of an attack -- a plain, immutable copy
///     taken by <c>Zone.ApplyCombatCommand</c> from a live <c>PlayerRuntimeState</c> (single-writer invariant,
///     architecture reference §10.1: the resolver itself never touches <c>PlayerRuntimeState</c>, it is pure
///     input-in/outcome-out, exactly like <see cref="Fenrir.Application.Game.Movement.MovementRules" />).
/// </summary>
public readonly record struct CombatantSnapshot(
    int CharacterId,
    byte Tribe,
    bool IsDead,
    int Life,
    int MaxLife,
    float PosX,
    float PosY,
    float PosZ,
    /// <summary>
    ///     Zone-clock instant this character last entered a zone -- a ONE-SHOT spawn/arrival grace period
    ///     (report 05 §4 point 1, legacy <c>mTickCountFor01SecondForProtect</c>, written only at registration/
    ///     zone-arrival, NEVER by combat itself -- see <see cref="PlayerRuntimeState.ZoneEntryAtZoneClock" />'s
    ///     own remarks). Null means "never entered a zone yet" -- deliberately NOT <see cref="TimeSpan.Zero" />
    ///     (which is a real, reachable zone-clock instant: a fresh zone or a player who entered before any tick
    ///     elapsed). Using zero as the "never" sentinel would incorrectly gate combat for every player during
    ///     the first <see cref="CombatResolver.ProtectDuration" /> of a zone's simulated lifetime.
    /// </summary>
    TimeSpan? ZoneEntryAtZoneClock,
    EffectiveStats Stats,
    /// <summary>
    ///     <c>gBuff(attacker)[8][0]</c> -- the "charge" buff percent (report 05 §4 point 3, buff slot 8, written
    ///     by skills 6/25/44 -- see <see cref="Fenrir.Application.Game.Skills.SkillEffectCatalog" />). Read (and,
    ///     on a successful attack, consumed to 0 by the caller) exactly once per attack, attacker-side only --
    ///     the legacy never reads the DEFENDER's charge buff.
    /// </summary>
    int ChargeBuffPercent);
