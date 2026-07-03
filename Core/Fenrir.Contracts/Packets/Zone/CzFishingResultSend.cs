using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_FISHING_RESULT_SEND (CLIENT.h:423-427) — fishing progress polling/forcing
///     (S04_MyWork02.cpp:13834-13891). Same zone-52 gating as CZ 103. <c>Sort</c> 1="is it biting?"
///     (requires step 3 + at least 1 minute since casting; 10% → step 4 [catch], 90% → step 5 [miss]),
///     2=recast (step → 2), 3=client forces <see cref="FishingStep" /> (clamped 0..5, else Quit). Other
///     <c>Sort</c> → Quit. <see cref="FishingStep" /> is only read when <c>Sort</c>=3. Answered by ZC 128
///     (<c>ZC_FISHING_RESULT_RECV</c>) unicast; on final step 4/5, rebroadcasts
///     <c>aAction.aSort=93</c> via the action channel. Also pushed server-side on line timeout
///     (S07_MyGame04.cpp:1322, gated <c>mServerNumber == 52</c>).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingResultSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzFishingResultSend : IIncomingPacket<CzFishingResultSend>
{
    public required int Sort { get; init; }
    public required int FishingStep { get; init; }
}
