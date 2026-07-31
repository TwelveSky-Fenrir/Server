using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInvite, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteRequest : IIncomingPacket<GuildInviteRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
