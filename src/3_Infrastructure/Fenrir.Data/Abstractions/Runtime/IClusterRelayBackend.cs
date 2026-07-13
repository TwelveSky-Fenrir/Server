using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
/// Contrat commun d'un dépôt de relais DB-outbox cross-shard : publier une entrée sortante
/// (<see cref="PublishAsync"/> → proc <c>runtime.*_Publish</c>) et sonder les lignes entrantes destinées à ce
/// shard (<see cref="PollAsync"/> → proc <c>runtime.*_Poll</c>, qui avance le curseur de lecture et retente sur
/// conflit d'écriture côté dépôt). C'est la signature que les 7 <c>I*RelayRepository</c> exposaient déjà de
/// facto ; l'extraire ici permet à <c>ClusterRelayPumpBase</c> de piloter n'importe quel topic sans connaître
/// son type d'entrée/DTO concret. Les tables et procédures durables restent la source de vérité, inchangées.
/// </summary>
/// <typeparam name="TEntry">Charge utile sortante publiée par le producteur (whisper, resync, siège…).</typeparam>
/// <typeparam name="TDto">Ligne entrante lue depuis l'outbox pour livraison locale.</typeparam>
public interface IClusterRelayBackend<TEntry, TDto>
{
    public ValueTask PublishAsync(TEntry entry, CancellationToken ct);

    public ValueTask<ImmutableArray<TDto>> PollAsync(byte shardId, int retentionSeconds, CancellationToken ct);
}
