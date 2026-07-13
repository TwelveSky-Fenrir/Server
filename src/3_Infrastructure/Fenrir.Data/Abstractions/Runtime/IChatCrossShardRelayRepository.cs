namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
/// Dépôt de relais des whispers cross-shard. Publish/Poll sont hérités tels quels de
/// <see cref="IClusterRelayBackend{TEntry,TDto}"/> (consolidation des 7 relais outbox, WS-C) ; le corps du dépôt
/// et ses procédures <c>runtime.usp_ChatCrossShardRelay_Publish/_Poll</c> restent inchangés.
/// </summary>
public interface IChatCrossShardRelayRepository
    : IClusterRelayBackend<ChatCrossShardWhisperEntry, ChatCrossShardWhisperDto>
{
}
