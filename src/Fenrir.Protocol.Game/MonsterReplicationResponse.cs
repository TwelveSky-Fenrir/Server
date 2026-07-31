using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MonsterReplication,
    ExpectedSize = 125)]
public readonly partial record struct MonsterReplicationResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required ObjectForMonster Data { get; init; }

    public required int CheckChangeActionState { get; init; }
}
