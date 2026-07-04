using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Dead in this build: every emission site is unreachable; kept because the client still decodes it.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeMigration,
    ExpectedSize = 5)]
public readonly partial record struct TribeMigrationResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
