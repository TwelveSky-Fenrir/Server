using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.WorldChat, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct WorldChatRequest : IIncomingPacket<WorldChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
