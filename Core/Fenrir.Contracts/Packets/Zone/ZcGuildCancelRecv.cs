using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_CANCEL_RECV (ZONE.h:833-835, empty payload, builder :1033) — unicast to the invitation
///     target whose invitation was cancelled by the emitter.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildCancelRecv, ExpectedSize = 1)]
public readonly partial record struct ZcGuildCancelRecv : IOutgoingPacket;
