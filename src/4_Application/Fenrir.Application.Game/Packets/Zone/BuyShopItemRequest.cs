using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyShopItem, ExpectedSize = 54,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyShopItemRequest : IIncomingPacket<BuyShopItemRequest>
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
