using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     WS1.4 target-shard delivery and asker-shard completion for cross-shard CZ_FRIEND_ASK_SEND/
///     CZ_FRIEND_ANSWER_SEND negotiations that <see cref="FriendService" /> itself could not complete locally
///     (same-shard <c>ZoneRegistry</c> miss, resolved via <see cref="ICharacterShardLocationRepository" /> --
///     see <see cref="FriendService.AskAsync" />'s own remarks). Registered as
///     <see cref="ISocialCrossShardRelayHandler" /> for <see cref="SocialCrossShardRelayKind.Friend" />;
///     <c>SocialCrossShardRelayHost</c> routes every delivered Ask/Answer row for that Kind here. The reason
///     codes this handler publishes on decline reuse the exact same numeric wire values
///     <c>FriendAskHandler</c> already sends for a same-shard rejection (3 = asker/target busy, 4 = target
///     not found) so the eventual <see cref="FriendAnswerResponse" /> the asker receives looks identical
///     whether the rejection happened synchronously (same-shard) or asynchronously (cross-shard, via this
///     handler).
/// </summary>
public sealed class FriendCrossShardRelayHandler(
    ZoneRegistry zones,
    FriendRegistry friends,
    DuelRegistry duels,
    TradeRegistry trades,
    PartyRegistry parties,
    GuildInviteRegistry guildInvites,
    MentorRegistry mentors,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<FriendCrossShardRelayHandler> logger) : ISocialCrossShardRelayHandler
{
    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.Friend;

    /// <summary>Runs on the TARGET's own shard.</summary>
    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        if (!zones.TryGetPlayer(ask.TargetCharacterId, out var target))
        {
            PublishDecline(ask, reasonCode: 4);
            return ValueTask.CompletedTask;
        }

        if (friends.IsNegotiating(target.CharacterId) || IsExcludedByCommunityWork(target))
        {
            PublishDecline(ask, reasonCode: 5);
            return ValueTask.CompletedTask;
        }

        if (!friends.TryRegisterCrossShardInbound(target.CharacterId,
                new CrossShardInboundAsk(ask.RelayId, ask.SourceShardId, ask.SourceCharacterId,
                    ask.SourceAvatarName)))
        {
            // Lost a narrow race against a same-shard ask that landed on the target in between the
            // IsNegotiating check above and this registration -- same "busy" reply as if it had been caught
            // synchronously.
            PublishDecline(ask, reasonCode: 5);
            return ValueTask.CompletedTask;
        }

        target.Session.Send(new FriendResponse { AvatarName = ask.SourceAvatarName });
        logger.LogDebug(
            "Cross-shard friend ask delivered: character {TargetId} <- asker {SourceCharacterId} on shard {SourceShardId}",
            target.CharacterId, ask.SourceCharacterId, ask.SourceShardId);
        return ValueTask.CompletedTask;
    }

    /// <summary>Runs on the original ASKER's own shard.</summary>
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

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new FriendAnswerResponse { Answer = accepted ? 0 : answer.ReasonCode ?? 1 });

        if (accepted)
            friends.MarkAccepted(askerId, answer.SourceCharacterId);

        logger.LogDebug(
            "Cross-shard friend answer delivered: asker {AskerId} <- target {TargetId} on shard {TargetShardId} (accepted={Accepted})",
            askerId, answer.SourceCharacterId, answer.SourceShardId, accepted);
        return ValueTask.CompletedTask;
    }

    private void PublishDecline(SocialCrossShardRelayDto ask, byte reasonCode)
    {
        crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
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

    /// <summary>
    ///     Duplicated from <see cref="FriendService" />'s own private method of the same name (Services
    ///     cannot share a private helper across classes without a new public surface neither needs elsewhere)
    ///     -- <c>CheckCommunityWork()</c>'s six OTHER exclusivity flags, re-run here against the TARGET's own
    ///     live state since the asker's shard could not evaluate it.
    /// </summary>
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
