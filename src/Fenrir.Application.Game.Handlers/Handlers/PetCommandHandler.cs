using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PetCommandHandler : IInlinePacketHandler<PetCommandRequest>
{
    public void Handle(in PetCommandRequest packet, IPacketSession session)
    {
    }
}
