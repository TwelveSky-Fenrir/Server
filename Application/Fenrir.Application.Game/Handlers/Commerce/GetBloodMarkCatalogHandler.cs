using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.Handlers.Commerce.Services;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>CZ_DEMAND_BLOOD_MARK_SEND (opcode 140) -- the blood-mark exchange catalog.</summary>
public sealed class GetBloodMarkCatalogHandler(IGetBloodMarkCatalogService service)
    : IInlinePacketHandler<GetBloodMarkCatalogRequest>
{
    public void Handle(in GetBloodMarkCatalogRequest packet, IPacketSession session)
    {
        session.Send(new GetBloodMarkCatalogResponse { Data = service.GetCatalog() });
    }
}
