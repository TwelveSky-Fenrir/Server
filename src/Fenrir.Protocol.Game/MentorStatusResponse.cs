using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorStatus, ExpectedSize = 5)]
public readonly partial record struct MentorStatusResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
