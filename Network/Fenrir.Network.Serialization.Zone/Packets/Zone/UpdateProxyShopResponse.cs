using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Value1 packs 6 item values + 3 sockets (9 ints), in that order.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UpdateProxyShop,
    ExpectedSize = 877)]
public readonly record struct UpdateProxyShopResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required ProxyShopUserInfo ProxyUser { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(9)] public required int[] Value1 { get; init; }
    public required int Money { get; init; }
}
