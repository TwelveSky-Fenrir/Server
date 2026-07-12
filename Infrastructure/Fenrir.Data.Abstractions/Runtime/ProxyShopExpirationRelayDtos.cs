using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record ProxyShopExpirationRelayEntry(
    byte SourceShardId,
    int CharacterId,
    int NewExpirationDate)
{
    // Idempotency token for usp_ProxyShopExpirationRelay_Publish's retry-safe dedup check -- see
    // GuildTribeBroadcastRelayEntry.CorrelationId's own remarks for the full rationale (generated once at
    // construction, stable across CrossShardRelayRetry's retries of this same entry instance).
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

// SourceShardId (present on ProxyShopExpirationRelayEntry) is deliberately absent here, not a gap --
// usp_ProxyShopExpirationRelay_Poll.sql never projects it, the column only backs that proc's own fan-out-
// exclusion predicate (`WHERE SourceShardId <> @ShardId`). See GuildTribeBroadcastRelayDto's own comment
// for the full reasoning and the point-to-point contrast (SocialCrossShardRelayDto et al.).
[GenerateDto]
public sealed partial record ProxyShopExpirationRelayDto(
    long RelayId,
    int CharacterId,
    int NewExpirationDate);
