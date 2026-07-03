using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_ANIMAL_STATE_RECV (ZONE.h:968-972) — unicast response to CZ 87, builder
///     <c>B_ANIMAL_STATE_RECV(tSort, tValue)</c> (S05_MyTransfer.cpp:1278-1283). <see cref="Sort" />
///     echoes the CZ 87 <c>tSort</c>; <see cref="Value" /> depends on the case — slot (1/2/5), echo (3),
///     0 (4), total attribute count after the operation (6/7/8).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AnimalStateRecv, ExpectedSize = 9)]
public readonly partial record struct ZcAnimalStateRecv : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
