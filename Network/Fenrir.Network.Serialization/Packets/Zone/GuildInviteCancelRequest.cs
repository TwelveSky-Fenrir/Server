using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Silently ignored unless the caller has a pending invitation to cancel.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInviteCancel, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteCancelRequest : IIncomingPacket<GuildInviteCancelRequest>;
