using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_HERORANK_INFO_SEND (CLIENT.h:476-478, shared empty typedef with CZ_HEROREWARD_SEND,
///     <c>S_HERORANK_INFO_SEND</c> CLIENT.h:725) — empty payload. Handler
///     <c>W_HERORANK_INFO_SEND</c> (S04_MyWork02.cpp:14159): conditionally replies with ZC 148
///     (previous ranking) and/or ZC 150 (current ranking), each gated by its own 2.5s throttle
///     (<c>mRankInfo->mPreviousUpdate</c>/<c>mCurrentUpdate</c> vs <c>mTickForRankingPre</c>/
///     <c>Cur</c>); if <c>mRankInfo</c> is absent or both throttles are unexpired: no reply at all. No
///     <c>Quit()</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.HeroRankInfoSend, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzHeroRankInfoSend : IIncomingPacket<CzHeroRankInfoSend>
{
}
