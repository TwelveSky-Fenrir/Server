using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Only a guild master/vice-master can send this; target must be in the same zone.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInvite, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteRequest : IIncomingPacket<GuildInviteRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
