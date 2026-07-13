using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UpdateProxyShop,
    ExpectedSize = 82, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct UpdateProxyShopRequest : IIncomingPacket<UpdateProxyShopRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int SellPage { get; init; }
    public required int SellIndex { get; init; }
    public required int SellItemIndex { get; init; }
    public required int SelfPage { get; init; }
    public required int SelfIndex { get; init; }
    public required int SelfX { get; init; }
    public required int SelfY { get; init; }
    public required int BuySort { get; init; }
    public required int Quantity { get; init; }
    public required int Value { get; init; }
    public required int Serial { get; init; }
    public required int Price { get; init; }
    [FixedArray(3)] public required int[] Socket { get; init; }
}
