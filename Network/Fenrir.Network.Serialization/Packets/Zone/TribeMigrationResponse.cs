using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Dead in this build: every emission site is unreachable; kept because the client still decodes it.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeMigration,
    ExpectedSize = 5)]
public readonly partial record struct TribeMigrationResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
