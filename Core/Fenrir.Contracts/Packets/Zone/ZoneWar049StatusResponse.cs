using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_049_TYPE_BATTLE_INFO (ZONE.h:924-928) — "zone 049" 4-tribe territory war tick, builder
///     S05_MyTransfer.cpp:1145, broadcast every tick (S07_MyGame01, 6 live emission sites).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar049Status,
    ExpectedSize = 21)]
public readonly partial record struct ZoneWar049StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] TribeUserNum { get; init; }
    public required int RemainTime { get; init; }
}
