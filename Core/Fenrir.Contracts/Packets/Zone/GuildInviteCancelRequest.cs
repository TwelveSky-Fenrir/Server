using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_CANCEL_SEND (CLIENT.h:155-161, empty struct) — cancels a pending guild invitation sent
///     by the emitter (requires <c>mGuildProcessState==1</c>, silent return otherwise, S04_MyWork02.cpp:9886).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInviteCancel, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteCancelRequest : IIncomingPacket<GuildInviteCancelRequest>;
