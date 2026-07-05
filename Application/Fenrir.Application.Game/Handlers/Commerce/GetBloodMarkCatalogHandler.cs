using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>CZ_DEMAND_BLOOD_MARK_SEND (opcode 140) -- the blood-mark exchange catalog.</summary>
public sealed class GetBloodMarkCatalogHandler(WorldDataCache worldData)
    : IInlinePacketHandler<GetBloodMarkCatalogRequest>
{
    public void Handle(in GetBloodMarkCatalogRequest packet, IPacketSession session)
    {
        session.Send(new GetBloodMarkCatalogResponse
        {
            Data = BloodShopBuilder.Build(worldData.BloodExchangeCatalog, worldData.ItemsById)
        });
    }
}
