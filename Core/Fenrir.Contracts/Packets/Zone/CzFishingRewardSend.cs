using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_FISHING_REWARD_SEND (CLIENT.h:428-430) — empty-payload signal packet claiming the fishing
///     catch (S04_MyWork02.cpp:13893-13958). Same zone-52 gating as CZ 103/104. Ignored silently unless
///     the player is in the "catch" state (<c>mFishingState</c> + <c>mCatchingFish</c>); on step 4, rolls
///     the fish (mounts/elixirs/tickets) and calls <c>SendItemToInventory</c>. Answered by ZC 129
///     (<c>ZC_FISHING_REWARD_RECV</c>, Result 1=ok / 2=inventory full) then ZC 128 (state echo), then
///     rebroadcasts <c>aAction.aSort=94/95</c> via the action channel.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingRewardSend, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzFishingRewardSend : IIncomingPacket<CzFishingRewardSend>
{
}
