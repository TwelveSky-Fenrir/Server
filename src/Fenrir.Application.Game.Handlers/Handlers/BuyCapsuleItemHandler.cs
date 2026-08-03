using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class BuyCapsuleItemHandler(ILogger<BuyCapsuleItemHandler> logger)
    : IInlinePacketHandler<BuyCapsuleItemRequest>
{
    public void Handle(in BuyCapsuleItemRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: BuyCapsuleItemRequest received — op114 P_CAPSULE_ITEM_BUY_SEND has no REGWORK1 line " +
            "in legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown " +
            "Header and quits the session (:292-301); silently ignored",
            session.SessionId);
    }
}
