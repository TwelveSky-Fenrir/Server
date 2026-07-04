using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Wire order is CashSize before Sort.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashBalance, ExpectedSize = 9)]
public readonly partial record struct GetCashBalanceResponse : IOutgoingPacket
{
    public required int CashSize { get; init; }
    public required int Sort { get; init; }
}
