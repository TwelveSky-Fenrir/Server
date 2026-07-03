using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_ASK_RECV (ZONE.h:827-830, builder <c>B_GUILD_ASK_RECV</c> S05_MyTransfer.cpp:1027) —
///     unicast to the invitation target, carrying the inviter's name.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildInvite, ExpectedSize = 14)]
public readonly partial record struct GuildInviteResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
