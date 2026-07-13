using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

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
