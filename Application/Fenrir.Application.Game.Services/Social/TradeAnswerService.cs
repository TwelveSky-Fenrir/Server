using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeAnswerService(
    TradeRegistry trades,
    ZoneRegistry zones,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<TradeAnswerService> logger) : ITradeAnswerService
{
    public TradeAnswerResult Answer(int targetId, int answer)
    {
        if (answer is not (0 or 1 or 2))
        {
            logger.LogDebug("Trade answer rejected: character {TargetId} sent malformed answer code {Answer}",
                targetId, answer);
            return new TradeAnswerResult(false, 0);
        }

        if (trades.TryConsumeCrossShardInbound(targetId, out var inbound))
        {
            var accepted = answer == 0;
            var targetName = zones.TryGetPlayer(targetId, out var targetState) ? targetState.Name : "";

            crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                SocialCrossShardRelayKind.Trade,
                SocialCrossShardRelayMessageType.Answer,
                accepted,
                null,
                options.Value.ShardId,
                targetId,
                targetName,
                inbound.SourceShardId,
                inbound.SourceCharacterId,
                inbound.RelayId));

            logger.LogDebug(
                "Trade answer (cross-shard): character {TargetId} answered {Answer} to asker {AskerId} on shard {AskerShardId}",
                targetId, answer, inbound.SourceCharacterId, inbound.SourceShardId);
            return new TradeAnswerResult(false, 0);
        }

        if (!trades.TryPeekPending(targetId, out var pendingAskerId, out var isAsker) || isAsker)
        {
            logger.LogDebug("Trade answer ignored: character {TargetId} has no pending ask", targetId);
            return new TradeAnswerResult(false, 0);
        }

        var askerBusyByZoneTransfer = !zones.TryGetPlayer(pendingAskerId, out var asker) || asker.IsMovingZone;

        if (!trades.TryAnswer(targetId, answer == 0, askerBusyByZoneTransfer, out var askerId, out var guardBlocked))
        {
            if (guardBlocked)
                logger.LogDebug(
                    "Trade answer: character {TargetId} answered but asker {AskerId} is unreachable or mid zone-transfer -- no notice sent, asker's own record left as-is",
                    targetId, askerId);
            else
                logger.LogDebug("Trade answer ignored: character {TargetId} has no pending ask", targetId);

            return new TradeAnswerResult(false, 0);
        }

        logger.LogDebug("Trade ask {Outcome}: character {TargetId} answered asker {AskerId}",
            answer == 0 ? "accepted" : "declined", targetId, askerId);
        return new TradeAnswerResult(true, askerId);
    }
}
