using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyKick, ExpectedSize = 14)]
public readonly partial record struct PartyKickResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
