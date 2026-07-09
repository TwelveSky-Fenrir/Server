using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Serialization.Zone.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_CLIENT_OK_FOR_ZONE_SEND (op13) -- the client's final ACK that it is ready for the zone it was just
///     registered into. Marks the session InWorld and runs the legacy handshake's 3 anti-cheat guardrails
///     (S04_MyWork02.cpp:1213-1291, see <see cref="IZoneReadyService" />), in the same order as the reference:
///     heartbeat watchdog, tribe anti-tamper, auto-hunt anti-hack.
/// </summary>
/// <remarks>
///     ZoneReadyRequest.AllowedStates spans Registering and InWorld -- the same blanket rule every other
///     incoming Zone opcode uses -- rather than Registering-only, so a repeat CLIENT_OK_FOR_ZONE_SEND no
///     longer gets rejected upstream by SessionStateGate with StateViolation the way it used to. Because of
///     that, this handler itself now owns the idempotency guard below: once a session is already InWorld, a
///     duplicate op13 is a safe no-op rather than re-running the 3 guardrails. That matters because Guard 3
///     (auto-hunt anti-hack) has a real side effect -- it increments <c>PlayerRuntimeState.AutoTimeHack</c>
///     -- and letting a harmless resend accumulate strikes could eventually Abort an already-legitimately-
///     playing session for no reason. The heartbeat watchdog is consequently reachable only via this repeat
///     path (no heartbeat can exist yet on the very first, Registering-state op13) but is wired for real,
///     exactly like legacy's own zone-transfer resend. Same "real but not yet reachable on the first call"
///     posture as several other guards in <see cref="PlayerRuntimeState" /> (e.g. MissionJoinWar).
///     This handler never sends any response packet, on any branch -- matching Server/ts25zone/
///     S04_MyWork02.cpp:1213-1291, where CLIENT_OK_FOR_ZONE_SEND has no outbound send anywhere in its body.
///     In particular, the avatar snapshot (EnterWorldResponse) and world/tribe broadcast
///     (WorldSnapshotResponse) are NOT sent from here: both already went out, unconditionally, inside the
///     op12 (EnterWorldRequest) handler that preceded this one -- see EnterWorldService's own remarks at its
///     two SendRaw calls. Do not add a send here to "complete the handshake"; that would both duplicate an
///     already-sent pair and reintroduce the exact op13-gating defect this comment exists to prevent.
/// </remarks>
public sealed class ZoneReadyHandler(IZoneReadyService service, ILogger<ZoneReadyHandler>? logger = null)
    : IInlinePacketHandler<ZoneReadyRequest>
{
    public void Handle(in ZoneReadyRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: ZoneReadyRequest (op13) received for character {CharacterId}, current state {State}, claimed tribe {ClaimedTribe}",
            session.SessionId, zoneSession.CharacterId, zoneSession.State, packet.Tribe);

        // Idempotency guard: the handshake already completed once for this session. Re-running the guards
        // below on a duplicate send would risk accumulating anti-hack strikes (Guard 3) against an already
        // in-world, legitimately playing session -- treat a repeat as a safe no-op instead.
        if (zoneSession.State == ZoneSessionState.InWorld)
        {
            logger?.LogDebug(
                "Zone-ready request ignored for character {CharacterId} (session {SessionId}): session is already InWorld",
                zoneSession.CharacterId, session.SessionId);
            return;
        }

        // Benign staleness window around world entry: the posted ZoneCommand.Enter may not have been ticked
        // yet. Nothing to validate against in that case -- fall through and still admit the session, same as
        // the handler's pre-existing (unconditional) behavior.
        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId &&
            zone.TryGetPlayer(characterId, out var state) && state is not null)
        {
            if (service.Validate(state, packet.Tribe, packet.AutoState) == ZoneReadyOutcome.Rejected)
            {
                logger?.LogWarning(
                    "Zone-ready handshake aborted for character {CharacterId} (session {SessionId})",
                    characterId, session.SessionId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }
        else
        {
            logger?.LogDebug(
                "Zone-ready validation skipped for session {SessionId}: player state not yet ticked into the zone (benign staleness window)",
                session.SessionId);
        }

        zoneSession.MarkInWorld();

        logger?.LogInformation(
            "Zone-ready handshake complete for character {CharacterId} (session {SessionId}) -- session is now InWorld",
            zoneSession.CharacterId, session.SessionId);
    }
}
