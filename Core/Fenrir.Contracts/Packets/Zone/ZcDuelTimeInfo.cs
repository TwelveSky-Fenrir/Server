using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelTimeInfo, ExpectedSize = 5)]
public readonly partial record struct ZcDuelTimeInfo : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
