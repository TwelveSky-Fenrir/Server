using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_PSHOP_ITEM_INFO_SEND (opcode 34), extracted from
///     <see cref="SearchShopListingsHandler" />.
/// </summary>
public interface ISearchShopListingsService
{
    /// <summary>
    ///     Market-wide search: every currently open proxy/deputy shop cluster-wide, unioned with every live
    ///     personal-shop stall currently open in this zone only (Server/ts25zone/S04_MyWork02.cpp:6523-6558
    ///     and :6559-6585 respectively). Returns one entry per matching listing (a burst, not a single
    ///     reply) -- the handler sends each in turn. Async because the proxy-shop half is a database read.
    /// </summary>
    public ValueTask<IReadOnlyList<SearchShopListingsResponse>> SearchAsync(SearchShopListingsRequest packet,
        Zone zone, CancellationToken cancellationToken);
}
