using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAllianceStatus, ExpectedSize = 9)]
public readonly partial record struct TribeAllianceStatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
