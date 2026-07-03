using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_DRUNK_INFO_RECV (ZONE.h:1275-1280) — builder <c>B_DRUNK_INFO_RECV(MyUser*, ...)</c>
///     (S05_MyTransfer.cpp:1782, USEND inline); reply to CZ 129 BOTTLE_STATE_SEND
///     (S04_MyWork02.cpp:14790/14797) — bottle/drunkenness system.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DrunkInfoRecv, ExpectedSize = 13)]
public readonly partial record struct ZcDrunkInfoRecv : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required int BottleIndex { get; init; }
}
