using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_PROCESS_QUEST_SEND (CLIENT.h:208-215, <c>S_PROCESS_QUEST_SEND</c> CLIENT.h:563) — handler
///     <c>W_PROCESS_QUEST_SEND</c> (S04_MyWork02.cpp:7307). <c>tSort = 1</c> requires
///     <c>ReturnQuestPresentState() == 1</c> and an existing next quest, else <c>Quit()</c>; for quest
///     types 3/6, <c>tXPost</c>/<c>tYPost</c> outside 0..7 or an already-occupied inventory slot →
///     <c>Quit()</c> (S04_MyWork02.cpp:7312-7348). Reply: ZC 39 (echo of all 5 fields,
///     S04_MyWork02.cpp:7560-7561).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.QuestProgress, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct QuestProgressRequest : IIncomingPacket<QuestProgressRequest>
{
    public required int Sort { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}
