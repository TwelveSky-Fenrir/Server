using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_FISHING_RESULT_RECV (ZONE.h:1181, SAME struct as ZC 127 via the typedef DOUBLE) — unicast
///     response to CZ 104, distinct C# contract, identical layout. Builder
///     <c>B_FISHING_RESULT_RECV(...)</c> with USEND baked in (S05_MyTransfer.cpp:1483-1492); also pushed
///     server-side on line timeout by the avatar loop (S07_MyGame04.cpp:1322, gated
///     <c>mServerNumber == 52</c>). Here <see cref="Result" /> = echo of CZ 104's <c>tSort</c> (1/2/3), or
///     1 on the reward/timeout path.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingResultRecv,
    ExpectedSize = 21)]
public readonly partial record struct ZcFishingResultRecv : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int Result { get; init; }
    public required int FishingState { get; init; }
    public required int FishingStep { get; init; }
}
