using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_ASK_SEND (CLIENT.h:315-319, struct shared with CZ_DEMAND_PSHOP/TRADE_ASK/etc.) — guild
///     invitation sent by a GM/AGM to a target avatar searched in the same zone (S04_MyWork02.cpp:9827).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInvite, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteRequest : IIncomingPacket<GuildInviteRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
