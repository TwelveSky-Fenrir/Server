using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_CLIENT_OK_FOR_ZONE_SEND (op13) -- the client's final ACK that it is ready for the zone it was just
///     registered into. Marks the session InWorld and runs the legacy handshake's 3 anti-cheat guardrails
///     (S04_MyWork02.cpp:1213-1291), in the same order as the reference: heartbeat watchdog, tribe anti-tamper,
///     auto-hunt anti-hack.
/// </summary>
/// <remarks>
///     Fenrir's op13 only ever fires once per session (ZoneReadyRequest.AllowedStates is Registering-only --
///     any later replay is already rejected upstream by SessionStateGate with StateViolation, unlike legacy
///     where nothing stops a repeat CLIENT_OK_FOR_ZONE_SEND). The heartbeat watchdog below is consequently
///     unreachable today (no heartbeat can exist yet before this handshake completes) but is wired for real:
///     it activates the moment anything re-admits op13 later in a session's life, exactly like legacy's own
///     zone-transfer resend. Same "real but not yet reachable" posture as several other guards in
///     <see cref="PlayerRuntimeState" /> (e.g. MissionJoinWar).
/// </remarks>
public sealed class ZoneReadyHandler : IInlinePacketHandler<ZoneReadyRequest>
{
    /// <summary>Legacy's <c>GetSecondFromTick(10)</c> heartbeat staleness window.</summary>
    private static readonly TimeSpan HeartbeatStaleWindow = TimeSpan.FromSeconds(10);

    /// <summary>Legacy's <c>mAutoTimeHack == 3</c> -- Quit() on the 3rd offense, not the 1st.</summary>
    private const int AutoHuntHackStrikeLimit = 3;

    public void Handle(in ZoneReadyRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // Benign staleness window around world entry: the posted ZoneCommand.Enter may not have been ticked
        // yet. Nothing to validate against in that case -- fall through and still admit the session, same as
        // the handler's pre-existing (unconditional) behavior.
        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId &&
            zone.TryGetPlayer(characterId, out var state) && state is not null)
        {
            // Guard 1: heartbeat watchdog. Only meaningful once a heartbeat has actually landed (mLastSentHeartbeat
            // != -1 in legacy) -- see the class remarks for why this never fires on Fenrir's current one-shot op13.
            if (state.LastSentHeartbeat is { } lastHeartbeat &&
                DateTime.UtcNow - lastHeartbeat > HeartbeatStaleWindow)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            // Guard 2: tribe anti-tamper. The client must echo back exactly the tribe world entry loaded from
            // the DB -- a mismatch means the client patched its own copy of the avatar's tribe.
            if (packet.Tribe != state.Tribe)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            // Guard 3: auto-hunt anti-hack. Legacy compares the client's claim against two server-held cash-item
            // timers (aAutoTime/aAutoTime2); Fenrir never modeled that cash timer (AvatarInfoTemplates hardcodes
            // both to 0), so the equivalent "does the server actually think auto-hunt is on" signal here is
            // AutoHuntEnabled (op99's own persisted toggle) -- a faithful re-grounding, not the literal fields.
            if (packet.AutoState > 0 && !state.AutoHuntEnabled)
            {
                state.AutoTimeHack++;
                if (state.AutoTimeHack >= AutoHuntHackStrikeLimit)
                {
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }
            }

            state.ConnectTime = DateTime.UtcNow;
        }

        zoneSession.MarkInWorld();
    }
}
