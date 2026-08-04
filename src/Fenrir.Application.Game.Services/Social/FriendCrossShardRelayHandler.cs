using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class FriendCrossShardRelayHandler(
    ZoneRegistry zones,
    FriendRegistry friends,
    DuelRegistry duels,
    TradeRegistry trades,
    PartyRegistry parties,
    GuildInviteRegistry guildInvites,
    MentorRegistry mentors,
    Lazy<ISocialCrossShardRelayQueue> crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<FriendCrossShardRelayHandler> logger) : ISocialCrossShardRelayHandler
{
    private const byte CrossShardUnavailableReason = 5;

    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.Friend;

    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        if (IsCrossShard(ask.SourceShardId))
        {
            friends.TryConsumeCrossShardInbound(ask.TargetCharacterId, out _);
            PublishDecline(ask, CrossShardUnavailableReason);
            logger.LogInformation(
                "Cross-shard friend ask declined: character {TargetId} <- asker {SourceCharacterId} on shard {SourceShardId}; a durable bilateral friendship transaction is unavailable",
                ask.TargetCharacterId, ask.SourceCharacterId, ask.SourceShardId);
            return ValueTask.CompletedTask;
        }

        if (!zones.TryGetPlayer(ask.TargetCharacterId, out var target))
        {
            PublishDecline(ask, 4);
            return ValueTask.CompletedTask;
        }

        if (friends.IsNegotiating(target.CharacterId) || IsExcludedByCommunityWork(target))
        {
            PublishDecline(ask, 5);
            return ValueTask.CompletedTask;
        }

        if (!friends.TryRegisterCrossShardInbound(target.CharacterId,
                new CrossShardInboundAsk(ask.RelayId, ask.SourceShardId, ask.SourceCharacterId,
                    ask.SourceAvatarName)))
        {
            PublishDecline(ask, 5);
            return ValueTask.CompletedTask;
        }

        target.Session.Send(new FriendResponse { AvatarName = ask.SourceAvatarName });
        logger.LogDebug(
            "Cross-shard friend ask delivered: character {TargetId} <- asker {SourceCharacterId} on shard {SourceShardId}",
            target.CharacterId, ask.SourceCharacterId, ask.SourceShardId);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct)
    {
        if (!friends.TryConsumeCrossShardOutbound(answer.TargetCharacterId, out _))
        {
            logger.LogInformation(
                "Cross-shard friend answer for asker {AskerId} has no matching pending ask -- asker already cancelled/disconnected, or a stale/duplicate Answer",
                answer.TargetCharacterId);
            return ValueTask.CompletedTask;
        }

        var askerId = answer.TargetCharacterId;
        var accepted = answer.Accepted == true;

        if (IsCrossShard(answer.SourceShardId))
        {
            friends.ClearAcceptedSelf(askerId);

            if (zones.TryGetPlayer(askerId, out var crossShardAsker))
                crossShardAsker.Session.Send(new FriendAnswerResponse { Answer = CrossShardUnavailableReason });

            logger.LogInformation(
                "Cross-shard friend answer declined: asker {AskerId} <- target {TargetId} on shard {TargetShardId}; pending state cleared without a local acceptance",
                askerId, answer.SourceCharacterId, answer.SourceShardId);
            return ValueTask.CompletedTask;
        }

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new FriendAnswerResponse { Answer = accepted ? 0 : answer.ReasonCode ?? 1 });

        if (accepted)
            friends.MarkAccepted(askerId, answer.SourceCharacterId);

        logger.LogDebug(
            "Cross-shard friend answer delivered: asker {AskerId} <- target {TargetId} on shard {TargetShardId} (accepted={Accepted})",
            askerId, answer.SourceCharacterId, answer.SourceShardId, accepted);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleCancelAsync(SocialCrossShardRelayDto cancel, CancellationToken ct)
    {
        if (!IsCrossShard(cancel.SourceShardId))
            return ValueTask.CompletedTask;

        if (!friends.TryConsumeCrossShardInbound(cancel.TargetCharacterId, out _))
        {
            logger.LogDebug(
                "Cross-shard friend cancel ignored for target {TargetId}: no pending inbound ask remains",
                cancel.TargetCharacterId);
            return ValueTask.CompletedTask;
        }

        logger.LogDebug(
            "Cross-shard friend pending ask cleared for target {TargetId} after cancellation from character {SourceCharacterId} on shard {SourceShardId}",
            cancel.TargetCharacterId, cancel.SourceCharacterId, cancel.SourceShardId);
        return ValueTask.CompletedTask;
    }

    private void PublishDecline(SocialCrossShardRelayDto ask, byte reasonCode)
    {
        crossShardRelay.Value.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.Friend,
            SocialCrossShardRelayMessageType.Answer,
            false,
            reasonCode,
            options.Value.ShardId,
            ask.TargetCharacterId,
            "",
            ask.SourceShardId,
            ask.SourceCharacterId,
            ask.RelayId));
    }

    private bool IsCrossShard(byte sourceShardId)
    {
        return sourceShardId != options.Value.ShardId;
    }

    private bool IsExcludedByCommunityWork(PlayerRuntimeState player)
    {
        return player.PshopOpen
               || duels.IsNegotiating(player.CharacterId)
               || trades.IsBusy(player.CharacterId)
               || parties.IsNegotiating(player.CharacterId)
               || guildInvites.IsNegotiating(player.CharacterId)
               || mentors.IsNegotiating(player.CharacterId);
    }
}
