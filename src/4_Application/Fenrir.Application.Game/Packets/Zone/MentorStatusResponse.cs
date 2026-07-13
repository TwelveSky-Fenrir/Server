using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorStatus, ExpectedSize = 5)]
public readonly partial record struct MentorStatusResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
