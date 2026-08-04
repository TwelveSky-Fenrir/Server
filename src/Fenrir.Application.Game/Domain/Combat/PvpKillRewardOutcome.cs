namespace Fenrir.Application.Game.Domain.Combat;

public enum PvpKillRewardOutcome : byte
{
    Ineligible,

    RejectedNoAuthoritativeCooldown,

    PendingAuthoritativeCooldown,

    Granted
}
