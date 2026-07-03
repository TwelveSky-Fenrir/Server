using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_PSHOP_ITEM_INFO_SEND (CLIENT.h:320-324) — market search across local + proxy shops.
///     <c>Sort1</c> = item type (EPTYPE_*, EPTYPE_ALL = all), <c>Sort2</c> = sub-category (EPSORT_*).
///     Answered by a burst of ZC_PSHOP_ITEM_INFO_RECV, one per matching listing.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SearchShopListings, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct SearchShopListingsRequest : IIncomingPacket<SearchShopListingsRequest>
{
    public required int Sort1 { get; init; }
    public required int Sort2 { get; init; }
}
