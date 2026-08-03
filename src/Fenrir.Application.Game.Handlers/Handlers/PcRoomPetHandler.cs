using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PcRoomPetHandler(ILogger<PcRoomPetHandler> logger)
    : IInlinePacketHandler<PcRoomPetRequest>
{
    public void Handle(in PcRoomPetRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: PcRoomPetRequest received — op136 P_PCROOM_PET_SEND has no REGWORK1 line in legacy " +
            "MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown Header and " +
            "quits the session (:292-301); replying with a canned failure",
            session.SessionId);

        session.Send(new PcRoomPetResponse
        {
            Result = 1,
            ItemIndex = 0,
            Page = 0,
            Index = 0,
            Xy = 0,
            Value = 0
        });
    }
}
