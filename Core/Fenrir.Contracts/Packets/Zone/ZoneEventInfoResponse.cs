using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_BROADCAST_INFO_RECV (ZONE.h:918-922) — 4 + 130 bytes since <c>USE_ITEM_LINK_V2</c> is ON
///     (<c>MAX_BROADCAST_DATA_SIZE = 130</c>). Builders <c>B_BROADCAST_INFO_RECV</c>/
///     <c>B_BROADCAST_INFO_RECV2</c> (S05_MyTransfer.cpp:1131/1138); zone-wide event-state broadcasts
///     (S07_MyGame02/08). <c>Sort</c> selects the application-level meaning of <see cref="Data" />, out
///     of wire scope.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneEventInfo,
    ExpectedSize = 135)]
public readonly partial record struct ZoneEventInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
}
