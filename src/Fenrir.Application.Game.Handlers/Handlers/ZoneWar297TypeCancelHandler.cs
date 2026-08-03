using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ZoneWar297TypeCancelHandler(ILogger<ZoneWar297TypeCancelHandler> logger)
    : IInlinePacketHandler<ZoneWar297TypeCancelRequest>
{
    public void Handle(in ZoneWar297TypeCancelRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: ZoneWar297TypeCancelRequest received — op100 P_297_TYPE_CANCEL_SEND has no REGWORK1 " +
            "line in legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown " +
            "Header and quits the session (:292-301); silently ignored",
            session.SessionId);
    }
}
