namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
/// Dépôt de relais social cross-shard (offres/réponses de négociation). Publish/Poll sont hérités tels quels de
/// <see cref="IClusterRelayBackend{TEntry,TDto}"/> (consolidation des 7 relais outbox, WS-C) ; le corps du dépôt
/// et ses procédures <c>runtime.usp_SocialCrossShardRelay_Publish/_Poll</c> restent inchangés.
/// </summary>
public interface ISocialCrossShardRelayRepository
    : IClusterRelayBackend<SocialCrossShardRelayEntry, SocialCrossShardRelayDto>
{
}
