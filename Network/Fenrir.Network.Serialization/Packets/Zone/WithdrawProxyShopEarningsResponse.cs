using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Result: 0=ok, 1-4=error (proxy state, currency caps, IPC).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WithdrawProxyShopEarnings,
    ExpectedSize = 13)]
public readonly partial record struct WithdrawProxyShopEarningsResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Money { get; init; }
    public required int BigMoney { get; init; }
}
