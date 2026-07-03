using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_ITEM_ACTION_RECV (ZONE.h:435-441, 96-byte payload: 4 + 4 + <see cref="ObjectForItem" /> (84) + 4) —
///     dropped-item replication (drop/pickup/despawn, periodic 5s logic tick). AOI broadcast. Trap: the 2 padding
///     bytes inside <see cref="ObjectForItem" /> (payload offsets 62-63, after PartyName) are part of the wire and
///     are covered by the generator's <c>ExpectedSize</c> check via <c>[Reserved(2)]</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GroundItemReplication, ExpectedSize = 97)]
public readonly partial record struct GroundItemReplicationResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required ObjectForItem Data { get; init; }
    public required int CheckChangeActionState { get; init; }
}
