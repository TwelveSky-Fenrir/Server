using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FourGuildChallengeDecisionHandler(ILogger<FourGuildChallengeDecisionHandler> logger)
    : IInlinePacketHandler<FourGuildChallengeDecisionRequest>
{
    public void Handle(in FourGuildChallengeDecisionRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: FourGuildChallengeDecisionRequest received — op107 " +
            "P_DECIDE_CHALLENGE_FOURGUILD_SEND has no REGWORK1 line in legacy MyWork::Init " +
            "(Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown Header and quits the " +
            "session (:292-301); silently ignored",
            session.SessionId);
    }
}
