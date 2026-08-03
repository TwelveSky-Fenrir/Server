using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftRecipe135Handler(ILogger<CraftRecipe135Handler> logger)
    : IInlinePacketHandler<CraftRecipe135Request>
{
    public void Handle(in CraftRecipe135Request packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: CraftRecipe135Request received — op135 P_MAKE_ITEM135_SEND has no REGWORK1 line in " +
            "legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266); only P_MAKE_ITEM2_SEND op131 is " +
            "registered (:138), so the legacy server logs Unknown Header and quits the session (:292-301); silently " +
            "ignored",
            session.SessionId);
    }
}
