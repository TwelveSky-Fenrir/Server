using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_PSHOP_ITEM_INFO_SEND (opcode 34), extracted from
///     <see cref="SearchShopListingsHandler" />.
/// </summary>
public interface ISearchShopListingsService
{
    /// <summary>
    ///     Market-wide search across every live personal-shop stall currently open in this zone. Returns one
    ///     entry per matching listing (a burst, not a single reply) -- the handler sends each in turn.
    /// </summary>
    public IReadOnlyList<SearchShopListingsResponse> Search(SearchShopListingsRequest packet, Zone zone);
}
