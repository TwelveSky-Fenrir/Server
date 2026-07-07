namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>Why an attack was refused before rolling -- each is a silent, packet-less <c>return;</c> in the legacy.</summary>
public enum AttackRejectReason
{
    None,
    SameCharacter,
    AttackerDead,
    DefenderDead,

    /// <summary>
    ///     Open-PvP (enemy-tribe) is disabled for the zone/map this attack is occurring in -- the
    ///     legacy-faithful gate at <c>AttackPlayer</c>'s ENEMY branch (S07_MyGame02.cpp:945-950). Duels are
    ///     never gated by this; it only applies to <see cref="CombatResolver.ResolveEnemyTribeAttack" />.
    /// </summary>
    ZonePvpDisabled,

    /// <summary>
    ///     Defender shares the attacker's tribe, OR the defender's tribe is the tribe currently allied with the
    ///     attacker's (<c>AttackPlayer</c>'s ENEMY branch, S07_MyGame02.cpp:954) -- both halves reproduced by
    ///     <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s <c>allyOfAttackerTribe</c> parameter. Skipped
    ///     entirely on the two FFA-exempt zones (see <see cref="ZonePvpZoneCatalog.IsSameTribeAttackExempt" />).
    /// </summary>
    SameOrAlliedTribe,
    OutOfRange,
    AttackerProtected,
    DefenderProtected,
    AttackerHasNoAttackSuccess,

    /// <summary>
    ///     Duel-specific authorization gate failed (S07_MyGame02.cpp:935-943): attacker and defender must both
    ///     be actively dueling, in the SAME active duel, with opposite roles. Only ever produced by
    ///     <see cref="CombatResolver.ResolveDuelAttack" />; <see cref="CombatResolver.ResolveEnemyTribeAttack" />
    ///     uses <see cref="ZonePvpDisabled" />/<see cref="SameOrAlliedTribe" /> instead (the contrasting gate at
    ///     :945-958) and never evaluates this one.
    /// </summary>
    DuelNotAuthorized,

    /// <summary>
    ///     <c>AttackPlayer</c>'s shared precondition block (S07_MyGame02.cpp:901-933, shop-open sub-check) --
    ///     the defender's player-shop is currently open. Same concept as
    ///     <see cref="StunRejectReason.DefenderShopOpen" />; reproduced by both
    ///     <see cref="CombatResolver.ResolveDuelAttack" /> and <see cref="CombatResolver.ResolveEnemyTribeAttack" />.
    /// </summary>
    DefenderShopOpen,

    /// <summary>
    ///     <c>CheckPossibleAttackTarget</c>'s avatar-target rule (S07_MyGame02.cpp:1692-1716, folded into
    ///     <c>AttackPlayer</c>'s own shared precondition block at 901-933): the defender's action-state is
    ///     exactly the "no action yet" placeholder (0) or the death pose (12) -- a second, independent gate
    ///     beyond <see cref="DefenderDead" />. Same concept as
    ///     <see cref="StunRejectReason.DefenderActionStateBlocksTargeting" />; reproduced by both
    ///     <see cref="CombatResolver.ResolveDuelAttack" /> and <see cref="CombatResolver.ResolveEnemyTribeAttack" />.
    /// </summary>
    DefenderActionStateBlocksTargeting,

    /// <summary>
    ///     Newbie-protection level gate failed: the current zone is one of the nine home-tribe-district
    ///     sub-zones gated by
    ///     <see cref="Fenrir.Application.Game.Domain.Combat.ZonePvpZoneCatalog.IsNewbieProtectionZone" />, the
    ///     attacker's level is &gt;= 90, and the defender's level is &lt; 90 (S07_MyGame02.cpp:960-976). Only
    ///     ever produced by <see cref="CombatResolver.ResolveEnemyTribeAttack" /> -- duels never evaluate it.
    /// </summary>
    NewbieProtectionLevelGap,

    /// <summary>
    ///     <c>tAttackInfo-&gt;mAttackActionValue1</c> was neither 1 (melee) nor 2 (skill) -- the attack-mode
    ///     selector's own <c>default: return;</c> branch (S07_MyGame02.cpp:2171-2189). Only ever produced by
    ///     <see cref="MonsterCombatResolver.ResolvePvmAttack" /> today.
    /// </summary>
    InvalidAttackModeSelector,

    /// <summary>
    ///     Only ever produced when the attack-mode selector is 2 (skill) and the attacker's own
    ///     <c>mCheckMaxAttackPacketNum</c> flag is set: the packet's skill number/combined grade must exactly
    ///     echo what the server currently has recorded for the attacker's own in-progress action
    ///     (S07_MyGame02.cpp:2177-2184). Same concept as <see cref="StunRejectReason.AntiCheatEchoMismatch" />,
    ///     but gated on a different per-session flag -- see
    ///     <see cref="MonsterCombatResolver.ResolvePvmAttack" />'s own remarks.
    /// </summary>
    AntiCheatEchoMismatch,

    /// <summary>
    ///     The target monster's owner-name field (<see cref="World.Monsters.MonsterEntity.OwnerName" />) is
    ///     non-empty and does not match the attacker's own name -- a player-summoned/owned monster locked to
    ///     whichever avatar it is stamped with (S07_MyGame02.cpp:1885-1896). One narrow exception in the
    ///     shipped LNW33 build: monster template 9002 is exempt from the name match itself, but still rejected
    ///     here until its own one-minute elapsed-time gate
    ///     (<see cref="World.Monsters.MonsterEntity.OwnerNameLockExemptionArmedAt" />) has passed. Only ever
    ///     produced by <see cref="MonsterCombatResolver.ResolvePvmAttack" /> today -- no Fenrir spawn path sets
    ///     <see cref="World.Monsters.MonsterEntity.OwnerName" /> yet, so this reason is currently unreachable
    ///     in production, reserved for when a summon-monster mechanic is implemented.
    /// </summary>
    OwnerNameLocked
}

/// <summary>A <see cref="Rejected" /> outcome carries no wire packet; a miss still echoes a zero-damage packet.</summary>
public readonly record struct AttackOutcome(
    bool Rejected,
    AttackRejectReason RejectReason,
    bool Hit,
    bool Critical,
    int DamageApplied,
    int ElementDamage,
    bool ChargeConsumed)
{
    public static AttackOutcome Reject(AttackRejectReason reason)
    {
        return new AttackOutcome(true, reason, false, false, 0, 0, false);
    }

    /// <summary>Charge buff is spent the moment an attack is attempted, win or miss -- callers with one must pass true.</summary>
    public static AttackOutcome Miss(bool chargeConsumed = false)
    {
        return new AttackOutcome(false, AttackRejectReason.None, false, false, 0, 0, chargeConsumed);
    }
}
