using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>No dedicated response record; state changes broadcast via AVATAR_CHANGE_INFO elsewhere, not ZC 102.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MountAbsorb, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MountAbsorbRequest : IIncomingPacket<MountAbsorbRequest>
{
    public required int Sort { get; init; }
}
