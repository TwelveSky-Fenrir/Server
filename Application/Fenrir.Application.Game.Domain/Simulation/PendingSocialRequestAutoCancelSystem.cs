using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PendingSocialRequestAutoCancelSystem(
    TradeRegistry tradeRegistry,
    FriendRegistry friendRegistry,
    MentorRegistry mentorRegistry,
    PartyRegistry partyRegistry,
    GuildInviteRegistry guildInviteRegistry,
    Lazy<ZoneRegistry> zoneRegistry) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            var characterId = state.CharacterId;

            SweepTrade(characterId);
            SweepFriend(characterId);
            SweepMentor(characterId);
            SweepParty(characterId);
            SweepGuildInvite(characterId);
        }
    }

    private bool IsReachable(int characterId)
    {
        return zoneRegistry.Value.TryGetPlayer(characterId, out _);
    }

    private void SweepTrade(int characterId)
    {
        if (!tradeRegistry.TryPeekPending(characterId, out var counterpartId, out var isAsker) ||
            IsReachable(counterpartId))
            return;

        if (isAsker)
            tradeRegistry.TryCancel(characterId, out _);
        else
            tradeRegistry.TryAnswer(characterId, false, out _);
    }

    private void SweepFriend(int characterId)
    {
        if (!friendRegistry.TryPeekPending(characterId, out var counterpartId, out var isAsker) ||
            IsReachable(counterpartId))
            return;

        if (isAsker)
            friendRegistry.TryCancel(characterId, out _);
        else
            friendRegistry.TryAnswer(characterId, false, out _);
    }

    private void SweepMentor(int characterId)
    {
        if (!mentorRegistry.TryPeekPending(characterId, out var counterpartId, out var isMaster) ||
            IsReachable(counterpartId))
            return;

        if (isMaster)
            mentorRegistry.TryCancel(characterId, out _);
        else
            mentorRegistry.TryAnswer(characterId, false, out _);
    }

    private void SweepParty(int characterId)
    {
        if (!partyRegistry.TryPeekPending(characterId, out var counterpartId, out var isInviter) ||
            IsReachable(counterpartId))
            return;

        if (isInviter)
            partyRegistry.TryCancel(characterId, out _);
        else
            partyRegistry.TryAnswer(characterId, false, false, out _, out _, out _);
    }

    private void SweepGuildInvite(int characterId)
    {
        if (!guildInviteRegistry.TryPeekPending(characterId, out var counterpartId, out var isAsker) ||
            IsReachable(counterpartId))
            return;

        if (isAsker)
            guildInviteRegistry.TryCancel(characterId, out _);
        else
            guildInviteRegistry.TryAnswer(characterId, false, out _);
    }
}
