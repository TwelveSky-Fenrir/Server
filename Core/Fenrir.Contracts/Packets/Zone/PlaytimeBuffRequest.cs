using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TIME_EFFECT_SEND (CLIENT.h:345-348, shared typedef with CZ_GET_CASH_SIZE_SEND/
///     CZ_PAT_ACTION_SEND/CZ_ANIMAL_ABSORB_SEND/CZ_RANK_BUFF_SEND/CZ_MISSION_COMPLETE_SEND,
///     <c>S_TIME_EFFECT_SEND</c> CLIENT.h:683) — handler <c>W_TIME_EFFECT_SEND</c>
///     (S04_MyWork02.cpp:12990). Forces <c>aPlayTime2 = 300</c> then sets
///     <c>aStateTimeEffect = tEffectTime[tSort-1]</c> if the play-time tier (1..5) is reached; other
///     values are ignored (no <c>Quit()</c>, unlike most handlers). Reply: ZC 22
///     (<c>S055BUFF_EFFECT_REWARD</c>, l.13011). Dedicated C# record despite the shared C++ typedef.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PlaytimeBuff, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PlaytimeBuffRequest : IIncomingPacket<PlaytimeBuffRequest>
{
    public required int Sort { get; init; }
}
