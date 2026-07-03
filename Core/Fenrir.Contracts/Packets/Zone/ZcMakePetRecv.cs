using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MAKE_PET_RECV (ZONE.h:985-989) — unicast response to CZ 88 (<c>CZ_MAKE_PET_SEND</c>, outside
///     this lot), builder <c>B_MAKE_PET_RECV(tResult, tValue[6])</c> (S05_MyTransfer.cpp:1298-1303).
///     <see cref="Result" /> in EU33: 0 = plain success (CMAKE_PET_4/5/6, the latter two under
///     <c>__GOD__</c>, active); 10000 = the LNW33 path of CMAKE_PET_1/2/3 (random pet or consolation pet
///     92291 for 1/2; guaranteed pet for 3, S04_MyWork02.cpp:12410). <see cref="Value" /> = the resulting
///     pet's inventory row (wInventory format: [0]=pet item index, [3]=activity/growth, e.g. 15 for the
///     consolation pet). Success is accompanied by a separate <c>MakeNotice</c> server announcement.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MakePetRecv, ExpectedSize = 29)]
public readonly partial record struct ZcMakePetRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
