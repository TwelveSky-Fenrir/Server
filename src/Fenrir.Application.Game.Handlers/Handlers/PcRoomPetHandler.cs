using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PcRoomPetHandler : IInlinePacketHandler<PcRoomPetRequest>
{
    public void Handle(in PcRoomPetRequest packet, IPacketSession session)
    {
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
