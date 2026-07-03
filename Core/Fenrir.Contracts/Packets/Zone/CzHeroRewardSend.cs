using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_HEROREWARD_SEND (CLIENT.h:476-478, same empty typedef as CZ 118, <c>S_HEROREWARD_SEND</c>
///     CLIENT.h:727) — empty payload. Handler <c>W_HEROREWARD_SEND</c> (S04_MyWork02.cpp:14183): looks
///     up the caller's <c>uCharIdx</c> in their tribe's top-10 (<c>mRankInfo->hCharIdx</c>, compared by
///     character id, not name); already claimed (<c>hAccept</c>) → ZC 149 <c>Result = 3</c>; otherwise
///     credits CP (<c>ProcessForCP</c>, LNW33 scale: rank 1 = 1000 CP … rank 10 = 100 CP), sets
///     <c>hAccept = 1</c> and replies ZC 149 <c>Result = 1000</c>. Unranked player: no reply at all.
///     Dedicated empty C# record — not merged with CZ 118.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.HeroRewardSend, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzHeroRewardSend : IIncomingPacket<CzHeroRewardSend>
{
}
