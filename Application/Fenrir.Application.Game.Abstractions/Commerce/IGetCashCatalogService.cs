using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_GET_CASH_ITEM_INFO_SEND (opcode 91), extracted from <see cref="GetCashCatalogHandler" />
///     .
/// </summary>
public interface IGetCashCatalogService
{
    /// <summary>
    ///     <paramref name="state" /> is null only if the requesting session couldn't be resolved to a live
    ///     zone player (a benign race around world-entry/transfer); the catalog is still returned, but
    ///     <see cref="PlayerRuntimeState.KnownCashCatalogVersion" /> then has nothing to record onto.
    /// </summary>
    public GetCashCatalogResponse GetCatalog(PlayerRuntimeState? state);
}
