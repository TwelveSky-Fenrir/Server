using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IHolyStoneCaptureRewardGateway
{
    public void GrantCaptureReward(PlayerRuntimeState capturer);

    public void GrantParticipationReward(PlayerRuntimeState tribemate);
}

public sealed class LoggingOnlyHolyStoneCaptureRewardGateway(ILogger<LoggingOnlyHolyStoneCaptureRewardGateway> logger)
    : IHolyStoneCaptureRewardGateway
{
    public void GrantCaptureReward(PlayerRuntimeState capturer)
    {
        logger.LogWarning(
            "HolyStoneWar: character {CharacterId} captured the Stone but no reward gateway is wired yet -- no currency/rank-point grant applied",
            capturer.CharacterId);
    }

    public void GrantParticipationReward(PlayerRuntimeState tribemate)
    {
        logger.LogWarning(
            "HolyStoneWar: character {CharacterId} qualifies for the tribe-wide participation grant but no reward gateway is wired yet",
            tribemate.CharacterId);
    }
}
