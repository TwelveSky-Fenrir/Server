using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Result: 0=ok, 3=stale-client mismatch/shop not closed/shop expired, 4=nothing to withdraw.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WithdrawProxyShopEarnings,
    ExpectedSize = 13)]
public readonly record struct WithdrawProxyShopEarningsResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
