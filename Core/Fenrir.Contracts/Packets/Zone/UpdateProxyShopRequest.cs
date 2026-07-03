using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_SEND (CLIENT.h:453-469) — edit the caller's deputy shop
///     (<c>mProxySystem.Process(..., 1)</c>): add a listing (<c>Sell*</c>), withdraw to self
///     (<c>Self*</c>), or buy (<c>BuySort</c>). No padding after <c>AvatarName[13]</c>: this packet is
///     pack(1) (unlike <see cref="ProxyShopUserInfo" />). Reply: ZC_SET_DEPUTY_PSHOP_RECV (plus
///     ZC 138/194 on the inventory side).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UpdateProxyShop,
    ExpectedSize = 82, AllowedStates = [(byte)ZoneSessionState.InWorld])]
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
