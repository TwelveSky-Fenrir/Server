using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ContinueSkillStatHandler(
    IContinueSkillStatService service,
    ILogger<ContinueSkillStatHandler> logger)
    : IInlinePacketHandler<ContinueSkillStatRequest>
{
    public void Handle(in ContinueSkillStatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: ContinueSkillStatRequest (op94) received for character {CharacterId}",
            session.SessionId, characterId);

        service.RegisterAutoBuffs(zone, characterId, state, packet.Skill);
        logger.LogInformation("Character {CharacterId} registered auto-buff skill slots", characterId);
        session.Send(new AutoBuffRegisterResponse { Value = 0 });
    }
}
