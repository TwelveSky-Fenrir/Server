using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_GET_CASH_ITEM_INFO_SEND (opcode 91), extracted from <see cref="GetCashCatalogHandler" />
///     .
/// </summary>
public interface IGetCashCatalogService
{
    public GetCashCatalogResponse GetCatalog();
}
