using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_TRADE_ASK_SEND attempt resolved.</summary>
public enum TradeInviteResultKind
{
    TargetNotFound,
    MustDisconnect,
    AskerBusy,
    TargetBusy,
    Sent,

    /// <summary>
    ///     WS1.4 ASK-PUBLISH-ONLY: the target was not found on this shard's own <c>ZoneRegistry</c> but WAS
    ///     resolved on a different live shard via <c>ICharacterShardLocationRepository</c> -- the invite has
    ///     been handed to <c>ISocialCrossShardRelayQueue</c>, but no <c>ISocialCrossShardRelayHandler</c> is
    ///     registered for <c>SocialCrossShardRelayKind.Trade</c> yet, so it is never actually delivered today
    ///     -- see <c>TradeInviteService.InviteAsync</c>'s own remarks.
    /// </summary>
    SentCrossShard
}

public readonly record struct TradeInviteResult(
    TradeInviteResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null,
    int AskerLevel = 0);

public interface ITradeInviteService
{
    /// <summary>
    ///     Same-shard lookup first (within <paramref name="zone" />), falling back to the cross-shard
    ///     character-location directory on a miss -- see <see cref="TradeInviteResultKind.SentCrossShard" />.
    /// </summary>
    public ValueTask<TradeInviteResult> InviteAsync(Zone zone, PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken);
}
