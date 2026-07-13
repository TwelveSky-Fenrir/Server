namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
/// Dépôt de relais des broadcasts guilde/tribu cross-shard. Publish/Poll sont hérités tels quels de
/// <see cref="IClusterRelayBackend{TEntry,TDto}"/> (consolidation des 7 relais outbox, WS-C) ; le corps du dépôt
/// et ses procédures <c>runtime.usp_GuildTribeBroadcastRelay_Publish/_Poll</c> restent inchangés.
/// </summary>
public interface IGuildTribeBroadcastRelayRepository
    : IClusterRelayBackend<GuildTribeBroadcastRelayEntry, GuildTribeBroadcastRelayDto>
{
}
