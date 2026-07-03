using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_CONTINUE_SKILL_USE_RECV (ZONE.h:1065-1068) — distinct struct from ZC 113 but identical
///     layout. Builder <c>B_AUTO_BUFF_USE_RECV</c> (S05_MyTransfer.cpp:1396); emitted from CZ 95
///     CONTINUE_SKILL_USE_SEND, <c>tSort == 1</c> branch only (S04_MyWork02.cpp:13045, value 0),
///     followed by a ZC 15 broadcast (animation aSort = 41).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ContinueSkillUseRecv,
    ExpectedSize = 5)]
public readonly partial record struct ZcContinueSkillUseRecv : IOutgoingPacket
{
    public required int Value { get; init; }
}
