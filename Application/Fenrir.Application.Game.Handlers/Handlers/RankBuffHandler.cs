using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class RankBuffHandler(IRankBuffService service, ILogger<RankBuffHandler> logger)
    : IInlinePacketHandler<RankBuffRequest>
{
    public void Handle(in RankBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: RankBuffRequest (op111) received for character {CharacterId}, sort {Sort}",
                session.SessionId, characterId, packet.Sort);

        var result = service.Apply(zone, state, characterId, packet.Sort);

        if (result.SilentlyIgnored)
        {
            logger.LogDebug(
                "Rank-buff silently ignored for character {CharacterId}: world tribe-symbol battle in progress",
                characterId);
            return;
        }

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Rank-buff rejected for character {CharacterId}: sort {Sort} (mid zone-transfer, out-of-range tier, or insufficient territory-symbol count) -- aborting session",
                characterId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        logger.LogInformation("Character {CharacterId} applied rank buff sort {Sort}", characterId, packet.Sort);

        session.Send(new AvatarStatUpdateResponse { Sort = 68, Value = packet.Sort, Value2 = 0 });
    }
}
