using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelCountdown, ExpectedSize = 5)]
public readonly partial record struct DuelCountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
