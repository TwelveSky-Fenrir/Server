using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PROCESS_QUEST_RECV (ZONE.h:577-587) — <c>#ifdef TS2_0 int tResult</c> is NOT compiled in EU33
///     (TS2_0 off) so the builder <c>B_PROCESS_QUEST_RECV</c> (S05_MyTransfer.cpp:705) serializes only
///     5 ints, discarding the builder's leading <c>tResult</c> argument (l.708). Response to CZ 36 plus
///     server-side quest events (S04_MyWork04, S07_MyGame01/02).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ProcessQuestRecv, ExpectedSize = 21)]
public readonly partial record struct ZcProcessQuestRecv : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}
