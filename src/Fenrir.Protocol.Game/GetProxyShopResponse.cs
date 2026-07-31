using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetProxyShop,
    ExpectedSize = 833)]
public readonly partial record struct GetProxyShopResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required ProxyShopUserInfo ProxyUser { get; init; }
}
