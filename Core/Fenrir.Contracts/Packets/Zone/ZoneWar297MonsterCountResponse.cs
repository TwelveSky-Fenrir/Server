using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297MonsterCount,
    ExpectedSize = 17)]
public readonly partial record struct ZoneWar297MonsterCountResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] MonsterNum { get; init; }
}
