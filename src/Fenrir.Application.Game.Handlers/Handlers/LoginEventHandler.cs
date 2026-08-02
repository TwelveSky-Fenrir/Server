using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class LoginEventHandler : IInlinePacketHandler<LoginEventRequest>
{
    public void Handle(in LoginEventRequest packet, IPacketSession session)
    {
    }
}
