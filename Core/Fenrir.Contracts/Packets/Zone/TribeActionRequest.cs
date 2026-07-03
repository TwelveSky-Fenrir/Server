using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TRIBE_WORK_SEND (CLIENT.h:273-277) — generic tribe sub-command channel (S04_MyWork02.cpp:10800),
///     entirely local zone + center shared memory (no extra RPC). <c>Data</c> is the raw 100-byte
///     <c>tData</c> buffer; its layout depends on <see cref="Sort" /> (tSort, doc 10 §2) and is
///     interpreted by the application layer. Answered with ZC_TRIBE_WORK_RECV, which echoes <c>Data</c>
///     back verbatim (not validated).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAction, ExpectedSize = 113,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeActionRequest : IIncomingPacket<TribeActionRequest>
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] Data { get; init; }
}
