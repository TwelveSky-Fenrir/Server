using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_CHANGE_TO_TRIBE4_RECV (ZONE.h:589-592) — registered-but-dead in EU33: builder
///     <c>B_CHANGE_TO_TRIBE4_RECV</c> (S05_MyTransfer.cpp:720) exists, but every call site
///     (S04_MyWork02.cpp:7572/7613/7621/7648/7757) sits after the unconditional
///     <c>tUserInfo->Quit(); return;</c> at the top of the CZ 37 handler, and the S04_MyWork03.cpp:5900
///     call is gated by <c>#ifdef TS20</c> (never defined). Specified because the client still decodes
///     it; no server path emits it in this build.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ChangeToTribe4Recv,
    ExpectedSize = 5)]
public readonly partial record struct ZcChangeToTribe4Recv : IOutgoingPacket
{
    public required int Result { get; init; }
}
