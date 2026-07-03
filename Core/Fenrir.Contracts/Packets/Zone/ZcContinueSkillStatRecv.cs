using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_CONTINUE_SKILL_STAT_RECV (ZONE.h:1060-1063) — builder named
///     <c>B_AUTO_BUFF_REGISTER_RECV</c> (S05_MyTransfer.cpp:1390); reply to CZ 94
///     CONTINUE_SKILL_STAT_SEND (S04_MyWork02.cpp:12985), acknowledges the 8 auto-buff registrations.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ContinueSkillStatRecv,
    ExpectedSize = 5)]
public readonly partial record struct ZcContinueSkillStatRecv : IOutgoingPacket
{
    public required int Value { get; init; }
}
