using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

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
