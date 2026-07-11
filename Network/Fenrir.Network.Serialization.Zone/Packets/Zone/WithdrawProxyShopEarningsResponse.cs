using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WithdrawProxyShopEarnings,
    ExpectedSize = 13)]
public readonly partial record struct WithdrawProxyShopEarningsResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
