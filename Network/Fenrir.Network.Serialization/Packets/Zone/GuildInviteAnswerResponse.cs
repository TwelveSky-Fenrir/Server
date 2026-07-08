using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Dual use: Answer is 0-2 (accept/refuse) to the target, or 3-5 (a pre-check failure) to the inviter.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildInviteAnswer, ExpectedSize = 5)]
public readonly record struct GuildInviteAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
