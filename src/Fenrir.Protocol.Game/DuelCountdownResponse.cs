using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelCountdown, ExpectedSize = 5)]
public readonly partial record struct DuelCountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
