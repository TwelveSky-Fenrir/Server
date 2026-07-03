using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_WORK_RECV (ZONE.h:874-879, builder <c>B_TRIBE_WORK_RECV</c> :1081-1087) — unicast
///     response to CZ_TRIBE_WORK_SEND (27 emission sites) plus one out-of-band emission (item 2193
///     "Reinforcement +1 Ticket" usage, S04_MyWork03.cpp:~6943, sends (0, 7, zeros) to refresh the halo
///     UI). <see cref="Data" /> is a raw echo of the client's <c>tData</c> (no server-side content),
///     except for that item-2193 case which sends zeros.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAction, ExpectedSize = 109)]
public readonly partial record struct TribeActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] Data { get; init; }
}
