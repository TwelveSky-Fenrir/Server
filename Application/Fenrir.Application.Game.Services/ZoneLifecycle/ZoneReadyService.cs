using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneReadyService(ILogger<ZoneReadyService>? logger = null) : IZoneReadyService
{
    /// <summary>Legacy's <c>mAutoTimeHack == 3</c> -- Quit() on the 3rd offense, not the 1st.</summary>
    private const int AutoHuntHackStrikeLimit = 3;

    /// <summary>Legacy's <c>GetSecondFromTick(10)</c> heartbeat staleness window.</summary>
    private static readonly TimeSpan HeartbeatStaleWindow = TimeSpan.FromSeconds(10);

    public ZoneReadyOutcome Validate(PlayerRuntimeState state, int tribe, int autoState)
    {
        // Guard 1: heartbeat watchdog. Only meaningful once a heartbeat has actually landed (mLastSentHeartbeat
        // != -1 in legacy) -- see the handler's own remarks for why this never fires on Fenrir's current
        // one-shot op13.
        if (state.LastSentHeartbeat is { } lastHeartbeat &&
            DateTime.UtcNow - lastHeartbeat > HeartbeatStaleWindow)
        {
            logger?.LogWarning("Zone-ready rejected for character {CharacterId}: heartbeat stale since {LastHeartbeat}",
                state.CharacterId, lastHeartbeat);
            return ZoneReadyOutcome.Rejected;
        }

        // Guard 2: tribe anti-tamper. The client must echo back exactly the tribe world entry loaded from
        // the DB -- a mismatch means the client patched its own copy of the avatar's tribe.
        if (tribe != state.Tribe)
        {
            logger?.LogWarning(
                "Zone-ready rejected for character {CharacterId}: claimed tribe {ClaimedTribe} does not match {ActualTribe}",
                state.CharacterId, tribe, state.Tribe);
            return ZoneReadyOutcome.Rejected;
        }

        // Guard 3: auto-hunt anti-hack. Legacy compares the client's claim against two server-held cash-item
        // timers (aAutoTime/aAutoTime2); Fenrir never modeled that cash timer (AvatarInfoTemplates hardcodes
        // both to 0), so the equivalent "does the server actually think auto-hunt is on" signal here is
        // AutoHuntEnabled (op99's own persisted toggle) -- a faithful re-grounding, not the literal fields.
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
