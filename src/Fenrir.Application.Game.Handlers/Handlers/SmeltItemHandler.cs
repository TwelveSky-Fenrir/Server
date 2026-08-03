using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SmeltItemHandler(ILogger<SmeltItemHandler> logger)
    : IInlinePacketHandler<SmeltItemRequest>
{
    public void Handle(in SmeltItemRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: SmeltItemRequest received — op102 P_SMELT_ITEM_SEND is registered only inside " +
            "#ifdef USE_REFINE (Server/ts25zone/S04_MyWork01.cpp:111-113), and USE_REFINE is #undef'd under LNW33 " +
            "(Server/Header/Protocol/DEFINE.h:106), so the shipped dispatcher entry stays NULL; replying with a " +
            "canned failure",
            session.SessionId);

        session.Send(new SmeltItemResponse { Result = 1, Cost = 0, Value = 0 });
    }
}
