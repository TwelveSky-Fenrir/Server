using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_FISHING_STATE_RECV (ZONE.h:1174-1181, typedef DOUBLE with ZC 128) — unicast response to CZ 103.
///     Alive only on the fishing zone (52), builder
///     <c>
///         B_FISHING_STATE_RECV(MyUser*, tServerIndex,
///         tUniqueNumber, tResult, tFishingState, tFishingStep)
///     </c>
///     with USEND baked in
///     (S05_MyTransfer.cpp:1472-1481).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingStateRecv, ExpectedSize = 21)]
public readonly partial record struct ZcFishingStateRecv : IOutgoingPacket
{
    /// <summary>Server-side avatar index.</summary>
    public required int ServerIndex { get; init; }

    public required uint UniqueNumber { get; init; }

    /// <summary>0=failure/no water, 1=line cast, 2=line reeled in.</summary>
    public required int Result { get; init; }

    /// <summary>0/1.</summary>
    public required int FishingState { get; init; }

    /// <summary>0..5.</summary>
    public required int FishingStep { get; init; }
}
