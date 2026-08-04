using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ContinueSkillUseHandler(IContinueSkillUseService service, ILogger<ContinueSkillUseHandler> logger)
    : IInlinePacketHandler<ContinueSkillUseRequest>
{
    public void Handle(in ContinueSkillUseRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        if (packet.Sort is not (1 or 2))
        {
            logger.LogWarning(
                "Session {SessionId}: ContinueSkillUseRequest (op95) carried out-of-range sort {Sort}, terminating session (Server/ts25zone/S04_MyWork02.cpp:12769-12771 default: Quit())",
                session.SessionId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: ContinueSkillUseRequest (op95) received for character {CharacterId}, sort {Sort}",
                session.SessionId, characterId, packet.Sort);

        var result = service.Activate(zone, characterId, state, packet.Sort);

        switch (result.Kind)
        {
            case AutoBuffActivationResolver.ResultKind.NoReply:
            case AutoBuffActivationResolver.ResultKind.Tick:
                return;

            case AutoBuffActivationResolver.ResultKind.Disconnect:
                logger.LogWarning(
                    "Auto-buff activation rejected for character {CharacterId}: insufficient mana on sort {Sort} -- ignoring (game-logic condition, not malformed wire input)",
                    characterId, packet.Sort);
                return;

            case AutoBuffActivationResolver.ResultKind.Activate:
                logger.LogInformation("Character {CharacterId} activated auto-buff channeling", characterId);
                session.Send(new AutoBuffActivationResponse { Value = 0 });
                return;
        }
    }
}
