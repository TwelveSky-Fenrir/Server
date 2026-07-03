namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Why a player died -- threaded through <c>Zone.ApplyDeath</c> so its (new) XP-loss branch can reproduce
///     the legacy's real, verified attribution instead of a plausible-sounding guess: report 05 §4 documents
///     XP loss as an EXCLUSIVELY <c>ProcessAttack04</c> (monster-vs-player) consequence -- reproduced directly
///     in <c>S07_MyGame02.cpp:3445-3489</c>. A PvP death (<c>ProcessAttack01/02</c>, <c>AttackPlayer</c>
///     l.1300-1373) instead calls <c>ProcessForKillOtherTribe</c> to reward the KILLER (CP/XP/drop/HSB, report
///     05 §6) -- it does NOT dock the victim's XP. Getting this backwards (docking XP on every death regardless
///     of cause) would be inventing behavior the source does not have.
/// </summary>
public enum DeathCause
{
    /// <summary>Anything not yet attributable (default) -- no XP-loss branch fires, matching "no MvP context = no loss".</summary>
    Unknown,

    /// <summary>ProcessAttack01/02 (duel or enemy-tribe PvP) -- reward pipeline for the killer is explicitly OUT of scope (report 05 §6, ~650-line ProcessForKillOtherTribe); not implemented here.</summary>
    PlayerKill,

    /// <summary>ProcessAttack04 (monster attacks the player) -- the ONLY cause that triggers the XP-loss formula (report 05 §4, §6).</summary>
    MonsterKill
}
