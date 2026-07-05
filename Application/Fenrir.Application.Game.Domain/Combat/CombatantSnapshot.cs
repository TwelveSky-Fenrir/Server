using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Immutable copy of one side of an attack, taken from a live <c>PlayerRuntimeState</c> -- the resolver is pure
///     input-in/outcome-out.
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
    /// <summary>Null means "never entered a zone" -- must not be <see cref="TimeSpan.Zero" />, a real reachable instant that would wrongly gate combat.</summary>
    TimeSpan? ZoneEntryAtZoneClock,
    EffectiveStats Stats,
    /// <summary>Attacker-side "charge" buff percent; the legacy never reads the defender's.</summary>
    int ChargeBuffPercent);
