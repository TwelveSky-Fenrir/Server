using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_SKY_UP_ITEM_RECV (ZONE.h:1053-1058) — reply to CZ_SKY_UP_ITEM_SEND (93); unicast. Same layout
///     as ZC 29/30/31 but a distinct C++ struct (not a shared typedef); registration gated by
///     <c>__REBIRTH__</c> (ACTIVE in EU33).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SkyUpItemRecv, ExpectedSize = 33)]
public readonly partial record struct ZcSkyUpItemRecv : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
