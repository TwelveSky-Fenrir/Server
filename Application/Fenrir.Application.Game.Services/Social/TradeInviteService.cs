using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeInviteService(
    TradeRegistry trades,
    DuelRegistry duels,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<TradeInviteService> logger) : ITradeInviteService
{

        public async ValueTask<TradeInviteResult> InviteAsync(Zone zone, PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        if (CommunityWorkGate.IsBusy(asker, duels, trades, friends, parties, mentors, guildInvites) ||
            asker.IsStunned || asker.IsDead)
        {
            logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy", asker.CharacterId);
            return new TradeInviteResult(TradeInviteResultKind.AskerBusy);
        }

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
            return await InviteCrossShardAsync(asker, targetAvatarName, cancellationToken).ConfigureAwait(false);

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != target.Tribe)
        {
            logger.LogWarning(
                "Trade invite rejected: character {AskerCharacterId} (tribe {AskerTribe}) targeted cross-tribe character {TargetCharacterId} (tribe {TargetTribe}) in map {MapId} -- session will be disconnected",
                asker.CharacterId, asker.Tribe, target.CharacterId, target.Tribe, zone.MapId);
            return new TradeInviteResult(TradeInviteResultKind.MustDisconnect);
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites) ||
            target.IsStunned || target.IsDead)
        {
            logger.LogDebug("Trade invite rejected: target character {TargetCharacterId} is busy",
                target.CharacterId);
            return new TradeInviteResult(TradeInviteResultKind.TargetBusy);
        }

        switch (trades.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case TradeAskOutcome.AskerBusy:
                logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy", asker.CharacterId);
                return new TradeInviteResult(TradeInviteResultKind.AskerBusy);
            case TradeAskOutcome.TargetBusy:
                logger.LogDebug("Trade invite rejected: target character {TargetCharacterId} is busy",
                    target.CharacterId);
                return new TradeInviteResult(TradeInviteResultKind.TargetBusy);
            default:
                logger.LogDebug(
                    "Trade invite sent: character {AskerCharacterId} ({AskerName}) -> character {TargetCharacterId} ({TargetName})",
                    asker.CharacterId, asker.Name, target.CharacterId, target.Name);
                return new TradeInviteResult(TradeInviteResultKind.Sent, target.CharacterId, target.Name, asker.Name,
                    asker.CombinedLevel);
        }
    }

        private async ValueTask<TradeInviteResult> InviteCrossShardAsync(PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Trade invite rejected: character {AskerCharacterId} target {TargetAvatarName} not found on any shard",
                asker.CharacterId, targetAvatarName);
            return new TradeInviteResult(TradeInviteResultKind.TargetNotFound);
        }

        var interTribeAllowed = asker.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != remote.Tribe)
        {
            logger.LogWarning(
                "Trade invite rejected: character {AskerCharacterId} (tribe {AskerTribe}) targeted cross-shard cross-tribe character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                asker.CharacterId, asker.Tribe, remote.CharacterId, remote.Tribe);
            return new TradeInviteResult(TradeInviteResultKind.MustDisconnect);
        }

        var outcome = trades.TryAskCrossShard(asker.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        if (outcome != TradeAskOutcome.Sent)
        {
            logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy (cross-shard registration)",
                asker.CharacterId);
            return new TradeInviteResult(TradeInviteResultKind.AskerBusy);
        }

        crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.Trade,
            SocialCrossShardRelayMessageType.Ask,
            null,
            null,
            options.Value.ShardId,
            asker.CharacterId,
            asker.Name,
            remote.ShardId,
            remote.CharacterId,
            null));

        logger.LogDebug(
            "Trade invite published cross-shard: character {AskerCharacterId} ({AskerName}) -> character {TargetCharacterId} on shard {TargetShardId} (never delivered today -- see TradeInviteService's own remarks)",
            asker.CharacterId, asker.Name, remote.CharacterId, remote.ShardId);
        return new TradeInviteResult(TradeInviteResultKind.SentCrossShard);
    }
}
