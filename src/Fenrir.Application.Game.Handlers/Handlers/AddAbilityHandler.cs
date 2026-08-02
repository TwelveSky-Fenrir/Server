using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class AddAbilityHandler(ILogger<AddAbilityHandler> logger)
    : IInlinePacketHandler<AddAbilityRequest>
{
    public void Handle(in AddAbilityRequest packet, IPacketSession session)
    {
        logger.LogDebug(
            "Session {SessionId}: AddAbilityRequest received — opcode superseded by GenericAction sort 206, silently ignored",
            session.SessionId);
    }
}
