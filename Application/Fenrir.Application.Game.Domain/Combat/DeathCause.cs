namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     XP loss only applies on a monster kill; a PvP death instead rewards the killer and does not dock the victim's
///     XP. Also the classification <see cref="World.Zone.ApplyDeath" /> uses to arm/disarm
///     <see cref="World.PlayerRuntimeState.ReviveHackFlag" /> (<c>mProtect_ReviveHack</c>,
///     <c>S07_MyGame04.cpp:1617-1624</c>): every cause arms it except <see cref="Duel" />.
/// </summary>
public enum DeathCause
{
    Unknown,
    PlayerKill,
    MonsterKill,

    /// <summary>
    ///     The team-stun sub-mechanic's cumulative-stun force-kill (<c>AVATAR_OBJECT::ProcessForDeath</c>,
    ///     <c>S07_MyGame04.cpp:1617-1658</c>, invoked from <c>S07_MyGame02.cpp:3727-3731</c> once
    ///     <see cref="World.PlayerRuntimeState.RepeatedStunCount" /> reaches 10) -- distinct from ordinary
    ///     HP-based death: grants no XP/loot/additional kill credit of its own (that all lives in the
    ///     damage-based death path this bypasses). Treated identically to every non-<see cref="MonsterKill" />
    ///     cause by <see cref="World.Zone.ApplyDeath" />'s own XP-loss gate -- no XP is docked either.
    /// </summary>
    StunLock,

    /// <summary>
    ///     A private 1-on-1 duel death (<c>PVP_ATTACK_TYPE::DUEL</c>, <c>S07_MyGame02.cpp:881-884</c>) -- the
    ///     one cause that does NOT arm <see cref="World.PlayerRuntimeState.ReviveHackFlag" />. Reached via
    ///     <c>Zone.ApplyDuelAttack</c> (<c>mCase</c> 1, <see cref="CombatResolver.ResolveDuelAttack" />), gated
    ///     by <see cref="Social.Duel.DuelRegistry" />'s own duel-authorization check rather than the ordinary
    ///     same-tribe guard <see cref="CombatResolver.ResolveEnemyTribeAttack" /> uses -- unlike that path, a
    ///     duel kill deliberately does NOT reach <c>Zone.ApplyPvpKillRewards</c> (see
    ///     <c>Zone.ApplyDuelAttack</c>'s own remarks for why).
    /// </summary>
    Duel
}
