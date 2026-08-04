using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeInviteService(
    TradeRegistry trades,
    DuelRegistry duels,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    WorldStateService worldState,
    ILogger<TradeInviteService> logger) : ITradeInviteService
{
    public ValueTask<TradeInviteResult> InviteAsync(Zone zone, PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken _)
    {
        if (CommunityWorkGate.IsBusy(asker, duels, trades, friends, parties, mentors, guildInvites) ||
            asker.IsStunned || asker.IsDead)
        {
            logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy", asker.CharacterId);
            return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.AskerBusy));
        }

        if (!zone.TryGetPlayerByName(targetAvatarName, out var target) || target is null)
            return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.TargetNotFound));

        if (target.CharacterId == asker.CharacterId)
        {
            logger.LogDebug("Trade invite rejected: character {AskerCharacterId} targeted its own avatar name",
                asker.CharacterId);
            return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.TargetNotFound));
        }

        var transition = trades.TryEnterTransition(asker.CharacterId, target.CharacterId);
        if (transition is null)
        {
            logger.LogDebug(
                "Trade invite rejected: character {AskerCharacterId} / target {TargetCharacterId} already have an in-flight transition",
                asker.CharacterId, target.CharacterId);
            return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.AskerBusy));
        }

        using (transition)
        {
            return InviteUnderTransition(zone, asker, target);
        }
    }

    private ValueTask<TradeInviteResult> InviteUnderTransition(Zone zone, PlayerRuntimeState asker,
        PlayerRuntimeState target)
    {
        var allyOfAskerTribe = worldState.GetAllyOf(asker.Tribe);
        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != target.Tribe && target.Tribe != allyOfAskerTribe)
        {
            logger.LogWarning(
                "Trade invite rejected: character {AskerCharacterId} (tribe {AskerTribe}) targeted cross-tribe character {TargetCharacterId} (tribe {TargetTribe}) in map {MapId} -- session will be disconnected",
                asker.CharacterId, asker.Tribe, target.CharacterId, target.Tribe, zone.MapId);
            return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.MustDisconnect));
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites) ||
            target.IsStunned || target.IsDead || target.IsMovingZone)
        {
            logger.LogDebug("Trade invite rejected: target character {TargetCharacterId} is busy",
                target.CharacterId);
            return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.TargetBusy));
        }

        switch (trades.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case TradeAskOutcome.AskerBusy:
                logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy", asker.CharacterId);
                return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.AskerBusy));
            case TradeAskOutcome.TargetBusy:
                logger.LogDebug("Trade invite rejected: target character {TargetCharacterId} is busy",
                    target.CharacterId);
                return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.TargetBusy));
            default:
                logger.LogDebug(
                    "Trade invite sent: character {AskerCharacterId} ({AskerName}) -> character {TargetCharacterId} ({TargetName})",
                    asker.CharacterId, asker.Name, target.CharacterId, target.Name);
                return ValueTask.FromResult(new TradeInviteResult(TradeInviteResultKind.Sent, target.CharacterId,
                    target.Name, asker.Name, asker.CombinedLevel));
        }
    }
}
