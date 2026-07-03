using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelCountdown, ExpectedSize = 5)]
public readonly partial record struct DuelCountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
