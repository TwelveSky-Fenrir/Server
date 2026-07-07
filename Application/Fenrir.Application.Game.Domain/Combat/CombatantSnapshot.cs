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
    int ChargeBuffPercent,
    /// <summary>
    ///     aLevel1, the character's raw base level -- the exact struct field <c>AttackPlayer</c> reads directly
    ///     at <c>Server/ts25zone/S07_MyGame02.cpp:971</c> for the newbie-protection level gate (see
    ///     <see cref="Fenrir.Application.Game.Domain.Combat.CombatResolver.ResolveEnemyTribeAttack" />'s
    ///     <c>newbieProtectionZone</c> parameter). NOT the same as the locally-recomputed "aLevel1 = Level+Level2"
    ///     combined value a differently-scoped local variable of the same name represents elsewhere (e.g.
    ///     <c>Server/ts25zone/S07_MyGame03.cpp:2660-2661</c>, <c>ProcessForKillOtherTribe</c>) -- those are two
    ///     distinct C++ symbols that happen to share an identifier, not the same convention.
    /// </summary>
    short Level = 0,
    /// <summary>
    ///     <c>World.PlayerRuntimeState.IsMovingZone</c> at snapshot time -- the Fenrir analog of legacy
    ///     <c>IsMovingZone()</c>, true only while this combatant is mid a CROSS-shard zone transfer (see that
    ///     field's own remarks). Currently consumed only by
    ///     <see cref="Fenrir.Application.Game.Domain.Combat.UnstunResolver.Resolve" /> (on its
    ///     <c>Target</c> only, never <c>Curer</c>, matching legacy) -- <see cref="CombatResolver" /> and
    ///     <see cref="MonsterCombatResolver" /> do not read this yet; see the
    ///     zone-transfer-in-progress-gate behavior contract (2026-07 round) for why those sites were left as
    ///     an open follow-up rather than guessed at here. Defaults to <c>false</c> so existing/test callers
    ///     that don't source this per-combatant fact keep prior behavior.
    /// </summary>
    bool IsMovingZone = false);
