using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyKick, ExpectedSize = 14)]
public readonly partial record struct PartyKickResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
