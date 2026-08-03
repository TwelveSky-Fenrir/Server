using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class RegisterTournamentHandler(ILogger<RegisterTournamentHandler> logger)
    : IInlinePacketHandler<RegisterTournamentRequest>
{
    public void Handle(in RegisterTournamentRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: RegisterTournamentRequest received — op150 P_REGISTER_TOURNAMENT_SEND is registered " +
            "only inside #ifdef TOURNAMENT_REGISTER (Server/ts25zone/S04_MyWork01.cpp:141-143), and " +
            "TOURNAMENT_REGISTER is commented out in the already-dead #else branch of #ifdef M33 " +
            "(Server/Header/Protocol/DEFINE.h:41), so the shipped dispatcher entry stays NULL; silently ignored",
            session.SessionId);
    }
}
