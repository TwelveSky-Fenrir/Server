using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UpdateProxyShop,
    ExpectedSize = 877)]
public readonly partial record struct UpdateProxyShopResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required ProxyShopUserInfo ProxyUser { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(9)] public required int[] Value1 { get; init; }
    public required int Money { get; init; }
}
