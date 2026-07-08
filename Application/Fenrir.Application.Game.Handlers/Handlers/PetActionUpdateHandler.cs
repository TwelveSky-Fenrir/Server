using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>CZ_UPDATE_PET_ACTION_SEND (op156). No reply -- position rebroadcasts via ZC 15.</summary>
public sealed class PetActionUpdateHandler(IPetActionUpdateService service, ILogger<PetActionUpdateHandler> logger)
    : IInlinePacketHandler<PetActionUpdateRequest>
{
    public void Handle(in PetActionUpdateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        // Fires on every pet position/action update while a companion pet is following its owner -- as
        // frequent as avatar movement, so this entry log must not format/box unconditionally; gate behind
        // IsEnabled like ContinueSkillUseHandler's own per-tick pulse.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: PetActionUpdateRequest (op156) received for character {CharacterId}",
                session.SessionId, zoneSession.CharacterId);

        var action = packet.Action;
        service.Apply(zone, zoneSession.CharacterId!.Value, in action);
    }
}
