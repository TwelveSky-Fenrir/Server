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

public sealed class GuildCrossShardRelayHandler(
    ZoneRegistry zones,
    GuildInviteRegistry guildInvites,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    Lazy<ISocialCrossShardRelayQueue> crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<GuildCrossShardRelayHandler> logger) : ISocialCrossShardRelayHandler
{
    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.GuildInvite;

    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        if (ask.AskRelayId is not { } correlationToken || correlationToken <= 0)
        {
            logger.LogInformation(
                "Cross-shard guild invite from character {SourceCharacterId} on shard {SourceShardId} has no correlation token; dropped",
                ask.SourceCharacterId, ask.SourceShardId);
            return ValueTask.CompletedTask;
        }

        if (!zones.TryGetPlayer(ask.TargetCharacterId, out var target))
        {
            PublishDecline(ask, 4);
            return ValueTask.CompletedTask;
        }

        if (target.GuildId is not null)
        {
            PublishDecline(ask, 5);
            return ValueTask.CompletedTask;
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites) ||
            target.IsStunned || target.IsDead || target.IsMovingZone)
        {
            PublishDecline(ask, 5);
            return ValueTask.CompletedTask;
        }

        if (!guildInvites.TryRegisterCrossShardInbound(target.CharacterId,
                new CrossShardInboundAsk(correlationToken, ask.SourceShardId, ask.SourceCharacterId,
                    ask.SourceAvatarName)))
        {
            PublishDecline(ask, 5);
            return ValueTask.CompletedTask;
        }

        target.Session.Send(new GuildInviteResponse { AvatarName = ask.SourceAvatarName });
        logger.LogDebug(
            "Cross-shard guild invite delivered: character {TargetId} <- asker {SourceCharacterId} on shard {SourceShardId}",
            target.CharacterId, ask.SourceCharacterId, ask.SourceShardId);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct)
    {
        if (answer.AskRelayId is not { } correlationToken || correlationToken <= 0 ||
            !guildInvites.TryConsumeCrossShardOutbound(answer.TargetCharacterId, answer.SourceShardId,
                answer.SourceCharacterId, correlationToken, out _))
        {
            logger.LogInformation(
                "Cross-shard guild invite answer for asker {AskerId} has no matching pending invitee, shard, and correlation -- cancelled, stale, or mismatched Answer",
                answer.TargetCharacterId);
            return ValueTask.CompletedTask;
        }

        var askerId = answer.TargetCharacterId;
        var accepted = answer.Accepted == true;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new GuildInviteAnswerResponse { Answer = accepted ? 0 : answer.ReasonCode ?? 1 });

        if (accepted)
            guildInvites.MarkAccepted(askerId, answer.SourceCharacterId);

        logger.LogDebug(
            "Cross-shard guild invite answer delivered: asker {AskerId} <- target {TargetId} on shard {TargetShardId} (accepted={Accepted})",
            askerId, answer.SourceCharacterId, answer.SourceShardId, accepted);
        return ValueTask.CompletedTask;
    }

    private void PublishDecline(SocialCrossShardRelayDto ask, byte reasonCode)
    {
        crossShardRelay.Value.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.GuildInvite,
            SocialCrossShardRelayMessageType.Answer,
            false,
            reasonCode,
            options.Value.ShardId,
            ask.TargetCharacterId,
            "",
            ask.SourceShardId,
            ask.SourceCharacterId,
            ask.AskRelayId));
    }
}
