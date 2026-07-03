using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_FISHING_STATE_SEND (CLIENT.h:417-422) — cast/reel the fishing line
///     (S04_MyWork02.cpp:13793-13831). Alive only on the <c>USE_FISHING</c> zone (runtime gate
///     <c>mSERVER_INFO.mServerNumber == 52</c>, S04_MyWork01.cpp:115-122) — Fenrir must register this
///     handler only for zone 52. <see cref="LocationX" />/<see cref="LocationZ" /> are DEAD FIELDS: the
///     handler never reads them (it uses the server-side position and checks water via
///     <c>mWORLD.GetYCoord</c>), but they are still part of the 21-byte wire layout and must be consumed.
///     <c>Sort</c> 1=cast line (fishing state → 1/step 2, rebroadcasts <c>aAction.aSort=92</c> via the
///     action channel), 2=reel in (state/step → 0). Other → Quit. Answered by ZC 127
///     (<c>ZC_FISHING_STATE_RECV</c>) unicast (Result 0=failure/no water, 1=cast, 2=reeled in).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingLine, ExpectedSize = 21,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct FishingLineRequest : IIncomingPacket<FishingLineRequest>
{
    public required int Sort { get; init; }
    public required float LocationX { get; init; }
    public required float LocationZ { get; init; }
}
