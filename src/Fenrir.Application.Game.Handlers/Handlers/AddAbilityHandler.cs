using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class AddAbilityHandler(ILogger<AddAbilityHandler> logger)
    : IInlinePacketHandler<AddAbilityRequest>
{
    public void Handle(in AddAbilityRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: AddAbilityRequest received — op132 P_ADD_ABILITY_SEND has no REGWORK1 line in " +
            "legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266) and is superseded by GenericAction sort " +
            "206 [STAT+] (Server/ts25zone/S04_MyWork04.cpp:420), so the legacy server logs Unknown Header and quits " +
            "the session (:292-301); silently ignored",
            session.SessionId);
    }
}
