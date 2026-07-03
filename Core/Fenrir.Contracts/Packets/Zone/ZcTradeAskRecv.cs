using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeAskRecv, ExpectedSize = 18)]
public readonly partial record struct ZcTradeAskRecv : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Level { get; init; }
}
