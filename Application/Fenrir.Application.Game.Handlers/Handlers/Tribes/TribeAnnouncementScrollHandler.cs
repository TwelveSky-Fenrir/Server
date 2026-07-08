using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_NOTIFY_SEND (opcode 112). Consumes one <see cref="PlayerRuntimeState.TribeNotifyScrollCount" />
///     charge and relays same-tribe, across every zone of this process (this build's <c>LNW33</c> branch;
///     the "send to everyone" branch is dead code here) -- unlike <see cref="TribeAnnouncementHandler" />
///     (op 80), there is no role gate at all. <see cref="TribeAnnouncementScrollResponse.TribeRole" /> on the
///     wire actually carries the sender's tribe number, not a role (see that contract's own docstring).
/// </summary>
public sealed class TribeAnnouncementScrollHandler(
    ITribeAnnouncementScrollService announcementService,
    ILogger<TribeAnnouncementScrollHandler>? logger = null) : IInlinePacketHandler<TribeAnnouncementScrollRequest>
{
    public void Handle(in TribeAnnouncementScrollRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_NOTIFY_SEND received (character {CharacterId}, content length {ContentLength})",
            session.SessionId, zoneSession.CharacterId, packet.Content.Length);

        if (string.IsNullOrEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        if (!announcementService.TryBroadcast(zone, sender, characterId, session, packet.Content))
            zoneSession.Abort(DisconnectReason.Faulted);
    }
}
