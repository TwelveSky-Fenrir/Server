using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Requires level >= 10 (anti-bot); broadcast to all players of every zone, no tribe filter.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.WorldChat, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct WorldChatRequest : IIncomingPacket<WorldChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
