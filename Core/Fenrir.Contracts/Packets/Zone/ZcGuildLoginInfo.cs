using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_LOGIN_INFO (ZONE.h:869-872, builder <c>B_GUILD_LOGIN_INFO</c> :1075-1079) — emitted by
///     the broadcast relay handler case 110 (S04_MyWork04.cpp:221-224): notifies every guild member
///     present in the zone that <see cref="AvatarName" /> just connected.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildLoginInfo, ExpectedSize = 14)]
public readonly partial record struct ZcGuildLoginInfo : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
