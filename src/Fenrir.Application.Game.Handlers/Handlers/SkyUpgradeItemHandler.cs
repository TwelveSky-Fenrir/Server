using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SkyUpgradeItemHandler(ILogger<SkyUpgradeItemHandler> logger)
    : IInlinePacketHandler<SkyUpgradeItemRequest>
{
    public void Handle(in SkyUpgradeItemRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: SkyUpgradeItemRequest received — op93 P_SKY_UP_ITEM_SEND has its REGWORK1 line " +
            "wrapped in #ifdef __REBIRTH__ (Server/ts25zone/S04_MyWork01.cpp:97-99), and __REBIRTH__ is defined only " +
            "at Server/Header/Protocol/DEFINE.h:28 inside the dead #else of #ifdef M33, so W_FUNCTION[93].PROC stays " +
            "NULL and the legacy server logs Unknown Header then quits the session (:292-301); silently ignored",
            session.SessionId);
    }
}
