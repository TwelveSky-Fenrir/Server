using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftPair134Handler(ILogger<CraftPair134Handler> logger)
    : IInlinePacketHandler<CraftPair134Request>
{
    public void Handle(in CraftPair134Request packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: CraftPair134Request received — op134 P_MAKE_ITEM134_SEND has no REGWORK1 line in " +
            "legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266); only P_MAKE_ITEM2_SEND op131 is " +
            "registered (:138), so the legacy server logs Unknown Header and quits the session (:292-301); silently " +
            "ignored",
            session.SessionId);
    }
}
