using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashBalance, ExpectedSize = 9)]
public readonly partial record struct GetCashBalanceResponse : IOutgoingPacket
{
    public required int CashSize { get; init; }
    public required int Sort { get; init; }
}
