using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Wire order is CashSize before Sort.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashBalance, ExpectedSize = 9)]
public readonly record struct GetCashBalanceResponse : IOutgoingPacket
{
    public required int CashSize { get; init; }
    public required int Sort { get; init; }
}
