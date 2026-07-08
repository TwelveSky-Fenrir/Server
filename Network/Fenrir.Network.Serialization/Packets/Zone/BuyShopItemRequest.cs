using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Page1/Index1 = seller's stall slot; Page2/Index2/XPost2/YPost2 = buyer's destination slot.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyShopItem, ExpectedSize = 54,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct BuyShopItemRequest : IIncomingPacket<BuyShopItemRequest>
{
    public required uint UniqueNumber { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    public required int Quantity1 { get; init; }
    public required int Page2 { get; init; }
    public required int Index2 { get; init; }
    public required int XPost2 { get; init; }
    public required int YPost2 { get; init; }
}
