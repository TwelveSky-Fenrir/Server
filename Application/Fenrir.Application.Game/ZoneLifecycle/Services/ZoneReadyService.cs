using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

public enum ZoneReadyOutcome
{
    Admitted,
    Rejected
}

/// <summary>
///     Business logic for CZ_CLIENT_OK_FOR_ZONE_SEND (op13)'s 3 anti-cheat guardrails
///     (S04_MyWork02.cpp:1213-1291), in the same order as the reference: heartbeat watchdog, tribe anti-tamper,
///     auto-hunt anti-hack. See <c>ZoneReadyHandler</c>'s own remarks for the full rationale.
/// </summary>
public interface IZoneReadyService
{
    ZoneReadyOutcome Validate(PlayerRuntimeState state, int tribe, int autoState);
}

public sealed class ZoneReadyService : IZoneReadyService
{
    /// <summary>Legacy's <c>GetSecondFromTick(10)</c> heartbeat staleness window.</summary>
    private static readonly TimeSpan HeartbeatStaleWindow = TimeSpan.FromSeconds(10);

    /// <summary>Legacy's <c>mAutoTimeHack == 3</c> -- Quit() on the 3rd offense, not the 1st.</summary>
    private const int AutoHuntHackStrikeLimit = 3;

    public ZoneReadyOutcome Validate(PlayerRuntimeState state, int tribe, int autoState)
    {
        // Guard 1: heartbeat watchdog. Only meaningful once a heartbeat has actually landed (mLastSentHeartbeat
        // != -1 in legacy) -- see the handler's own remarks for why this never fires on Fenrir's current
        // one-shot op13.
        if (state.LastSentHeartbeat is { } lastHeartbeat &&
            DateTime.UtcNow - lastHeartbeat > HeartbeatStaleWindow)
            return ZoneReadyOutcome.Rejected;

        // Guard 2: tribe anti-tamper. The client must echo back exactly the tribe world entry loaded from
        // the DB -- a mismatch means the client patched its own copy of the avatar's tribe.
        if (tribe != state.Tribe)
            return ZoneReadyOutcome.Rejected;

        // Guard 3: auto-hunt anti-hack. Legacy compares the client's claim against two server-held cash-item
        // timers (aAutoTime/aAutoTime2); Fenrir never modeled that cash timer (AvatarInfoTemplates hardcodes
        // both to 0), so the equivalent "does the server actually think auto-hunt is on" signal here is
        // AutoHuntEnabled (op99's own persisted toggle) -- a faithful re-grounding, not the literal fields.
        if (autoState > 0 && !state.AutoHuntEnabled)
        {
            state.AutoTimeHack++;
            if (state.AutoTimeHack >= AutoHuntHackStrikeLimit)
                return ZoneReadyOutcome.Rejected;
        }

        state.ConnectTime = DateTime.UtcNow;
        return ZoneReadyOutcome.Admitted;
    }
}
