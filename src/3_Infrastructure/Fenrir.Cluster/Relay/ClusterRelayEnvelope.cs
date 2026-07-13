using System;

namespace Fenrir.Cluster.Relay;

/// <summary>
/// Enveloppe d'un événement cross-zone. Le <see cref="CorrelationId"/> porte l'<b>idempotence</b> (un événement
/// rejoué est neutre). <see cref="TargetZone"/> nul = fan-out à toutes les zones ; renseigné = unicast.
/// </summary>
/// <typeparam name="TEvent">Charge utile de l'événement (party, guilde, whisper, notice, siège…).</typeparam>
public readonly record struct ClusterRelayEnvelope<TEvent>(
    Guid CorrelationId,
    short SourceZone,
    short? TargetZone,
    TEvent Payload)
    where TEvent : notnull;
