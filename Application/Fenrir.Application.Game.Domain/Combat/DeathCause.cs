namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     XP loss only applies on a monster kill; a PvP death instead rewards the killer and does not dock the victim's
///     XP.
/// </summary>
public enum DeathCause
{
    Unknown,
    PlayerKill,
    MonsterKill
}
