using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PROCESS_DATA_RECV (ZONE.h:462-468, 142-byte payload: 4 + 4 + 130 + 4) — reply to CZ_PROCESS_ATTACK_SEND's
///     sibling CZ_PROCESS_DATA_SEND (<c>ProcessForData</c>), a generic-channel applicative blob keyed by
///     <c>tSort</c> (e.g. animal-absorption state, tSort=7997). Unicast. Trap: 130 bytes (not 100) because
///     <c>USE_ITEM_LINK_V2</c> is ON in EU33 -&gt; <c>MAX_BROADCAST_DATA_SIZE = 130</c>. The content of
///     <see cref="Data" /> is an application sub-protocol keyed by <see cref="Sort" />, out of scope for the wire
///     contract.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ProcessDataRecv, ExpectedSize = 143)]
public readonly partial record struct ZcProcessDataRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
    public required int RuneValue { get; init; }
}
