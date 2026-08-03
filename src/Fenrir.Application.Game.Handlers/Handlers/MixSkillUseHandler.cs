using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class MixSkillUseHandler(ILogger<MixSkillUseHandler> logger)
    : IInlinePacketHandler<MixSkillUseRequest>
{
    public void Handle(in MixSkillUseRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: MixSkillUseRequest received — op128 P_MIXSKILL_USE_SEND has no REGWORK1 line in " +
            "legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown Header " +
            "and quits the session (:292-301); silently ignored",
            session.SessionId);
    }
}
