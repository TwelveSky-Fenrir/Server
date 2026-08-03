using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SocketSystemHandler(ILogger<SocketSystemHandler> logger)
    : IInlinePacketHandler<SocketSystemRequest>
{
    public void Handle(in SocketSystemRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: SocketSystemRequest received — op98 P_SOCKET_SYSTEM_SEND is registered only inside " +
            "#ifdef USE_SOCKET_GEM (Server/ts25zone/S04_MyWork01.cpp:104-106), and USE_SOCKET_GEM is #undef'd under " +
            "LNW33 (Server/Header/Protocol/DEFINE.h:105), so the shipped dispatcher entry stays NULL; silently ignored",
            session.SessionId);
    }
}
