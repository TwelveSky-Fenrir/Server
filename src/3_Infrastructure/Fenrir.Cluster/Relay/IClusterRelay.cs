using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Cluster.Relay;

/// <summary>
/// Abstraction <b>unique</b> de relais cross-zone (correctif F4 : consolide les 7 relais outbox quasi-copiés du
/// C# actuel en une seule API). Deux régimes derrière la même interface : <b>push TCP</b> pour le temps réel
/// (whisper/party/guilde/notice/handoff — fin de la latence de poll) et <b>DB-outbox transactionnel</b> pour le
/// durable (économie/mail/audit). Voir <c>Roadmap/FONDATIONS/05_CenterServer_Coordination.md</c>.
/// </summary>
/// <typeparam name="TEvent">Type d'événement relayé.</typeparam>
public interface IClusterRelay<TEvent>
    where TEvent : notnull
{
    /// <summary>Publie un événement (fan-out ou unicast selon <see cref="ClusterRelayEnvelope{TEvent}.TargetZone"/>).</summary>
    ValueTask PublishAsync(ClusterRelayEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}

/// <summary>Consommateur d'un événement cross-zone reçu (côté zone destinataire).</summary>
/// <typeparam name="TEvent">Type d'événement relayé.</typeparam>
public interface IClusterRelayHandler<TEvent>
    where TEvent : notnull
{
    /// <summary>Traite un événement livré (idempotent sur <see cref="ClusterRelayEnvelope{TEvent}.CorrelationId"/>).</summary>
    ValueTask HandleAsync(ClusterRelayEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}
