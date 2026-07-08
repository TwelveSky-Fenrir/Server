using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MonsterReplication,
    ExpectedSize = 125)]
public readonly record struct MonsterReplicationResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required ObjectForMonster Data { get; init; }

    /// <summary>0/1/2: forces animation re-sync client-side.</summary>
    public required int CheckChangeActionState { get; init; }
}
