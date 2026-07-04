using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// PshopItemInfo: [0]=itemID [1]=qty [2]=value [3]=serial [4]=price, [5..8]=0 (page/x/y unused in market view).
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
