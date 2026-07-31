using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashBalance, ExpectedSize = 9)]
public readonly partial record struct GetCashBalanceResponse : IOutgoingPacket
{
    public required int CashSize { get; init; }
    public required int Sort { get; init; }
}
