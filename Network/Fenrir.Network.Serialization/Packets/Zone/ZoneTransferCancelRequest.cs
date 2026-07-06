using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Only valid while the session's "moving zone" flag is set; implement as a flag check, not a dedicated session
///     state.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneTransferCancel, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneTransferCancelRequest : IIncomingPacket<ZoneTransferCancelRequest>
{
}
