using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_DEMAND_BLOOD_MARK_SEND (opcode 140, contracts/04_commerce.md, USE_BLOOD family) -- the blood-mark
///     exchange catalog (world.BloodExchangeCatalog, already scaffolded). Built once per request from
///     <see cref="BloodShopBuilder" /> (cheap: 2 real rows) -- see that type's own remarks for the
///     "BloodNum is unconditionally 50" correction to the contract doc's own (wrong) paraphrase.
/// </summary>
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
