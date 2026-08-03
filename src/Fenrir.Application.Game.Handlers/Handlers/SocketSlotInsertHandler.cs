using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SocketSlotInsertHandler(ILogger<SocketSlotInsertHandler> logger)
    : IInlinePacketHandler<SocketSlotInsertRequest>
{
    public void Handle(in SocketSlotInsertRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: SocketSlotInsertRequest received — op121 P_SOCKET_SLOT_INSERT_SEND is registered " +
            "only inside #ifdef USE_SOCKET_GEM (Server/ts25zone/S04_MyWork01.cpp:131-133), and USE_SOCKET_GEM is " +
            "#undef'd under LNW33 (Server/Header/Protocol/DEFINE.h:105), so the shipped dispatcher entry stays NULL; " +
            "replying with a canned failure",
            session.SessionId);

        session.Send(new SocketSlotInsertResponse { Result = 1, Value = [0, 0, 0] });
    }
}
