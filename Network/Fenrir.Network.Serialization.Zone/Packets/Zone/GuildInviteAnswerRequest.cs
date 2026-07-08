using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Answer: 0=accept, 1/2=refuse; any other value is silently ignored.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInviteAnswer, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteAnswerRequest : IIncomingPacket<GuildInviteAnswerRequest>
{
    public required int Answer { get; init; }
}
