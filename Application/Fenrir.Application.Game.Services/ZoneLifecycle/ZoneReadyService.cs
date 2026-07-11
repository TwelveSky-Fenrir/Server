using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneReadyService(ILogger<ZoneReadyService>? logger = null) : IZoneReadyService
{

        private const int AutoHuntHackStrikeLimit = 3;

        private static readonly TimeSpan HeartbeatStaleWindow = TimeSpan.FromSeconds(10);

    public ZoneReadyOutcome Validate(PlayerRuntimeState state, int tribe, int autoState)
    {
        if (state.LastSentHeartbeat is { } lastHeartbeat &&
            DateTime.UtcNow - lastHeartbeat > HeartbeatStaleWindow)
        {
            logger?.LogWarning("Zone-ready rejected for character {CharacterId}: heartbeat stale since {LastHeartbeat}",
                state.CharacterId, lastHeartbeat);
            return ZoneReadyOutcome.Rejected;
        }

        if (tribe != state.Tribe)
        {
            logger?.LogWarning(
                "Zone-ready rejected for character {CharacterId}: claimed tribe {ClaimedTribe} does not match {ActualTribe}",
                state.CharacterId, tribe, state.Tribe);
            return ZoneReadyOutcome.Rejected;
        }

        if (autoState > 0 && !state.AutoHuntEnabled)
        {
            state.AutoTimeHack++;
            if (state.AutoTimeHack >= AutoHuntHackStrikeLimit)
            {
                logger?.LogWarning(
                    "Zone-ready rejected for character {CharacterId}: auto-hunt anti-hack strike limit reached ({Strikes})",
                    state.CharacterId, state.AutoTimeHack);
                return ZoneReadyOutcome.Rejected;
            }
        }

        state.ConnectTime = DateTime.UtcNow;
        return ZoneReadyOutcome.Admitted;
    }
}
