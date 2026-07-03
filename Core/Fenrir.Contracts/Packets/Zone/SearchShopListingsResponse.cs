using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PSHOP_ITEM_INFO_RECV (ZONE.h:556-565) — reply to CZ_PSHOP_ITEM_INFO_SEND, sent as a burst (one
///     packet per matching listing, local + proxy shops), sharing a common <c>CycleTick</c>.
///     <c>PshopItemInfo</c> = [0]=itemID, [1]=quantity, [2]=value, [3]=serial, [4]=price, [5..8]=0 (the
///     page/x/y fields of PSHOP_INFO are zeroed in this market view). <c>Page</c>/<c>Index</c> = the
///     source stall slot. No padding: pack(1) packet. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SearchShopListings,
    ExpectedSize = 78)]
public readonly partial record struct SearchShopListingsResponse : IOutgoingPacket
{
    public required uint UniqueNumber { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(9)] public required int[] PshopItemInfo { get; init; }
    [FixedArray(3)] public required int[] SocketInfo { get; init; }
    public required uint CycleTick { get; init; }
}
