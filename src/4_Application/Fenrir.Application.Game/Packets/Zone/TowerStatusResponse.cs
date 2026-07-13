using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TowerStatus,
    ExpectedSize = 97)]
public readonly partial record struct TowerStatusResponse : IOutgoingPacket
{
    [FixedArray(12)] public required int[] State1Tower { get; init; }
    [FixedArray(12)] public required int[] State2Tower { get; init; }
}
