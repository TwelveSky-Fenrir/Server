using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_MAKE_SKILL_SEND (CLIENT.h:289) — same typedef as <see cref="CzMakeItemSend" /> (29). Skill-book
///     crafting from 4 fragments (1054-1057 -> 90567, 1058-1061 -> 90568, 1062-1065 -> 90569). The "War
///     God" recipe (<c>tSort</c> 3, fragments 99301-99304) is ALIVE in EU33: the compiled branch is
///     <c>__REBIRTH__</c> (active), not the <c>#else</c> Quit-only branch (S04_MyWork02.cpp:5976-5988).
///     Response: ZC 33.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MakeSkillSend, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzMakeSkillSend : IIncomingPacket<CzMakeSkillSend>
{
    public required int Sort { get; init; }

    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Page3 { get; init; }

    public required int Index3 { get; init; }

    public required int Page4 { get; init; }

    public required int Index4 { get; init; }
}
