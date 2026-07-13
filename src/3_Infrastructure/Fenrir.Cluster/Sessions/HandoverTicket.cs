using System;

namespace Fenrir.Cluster.Sessions;

/// <summary>
/// Ticket de handover <b>single-use</b>, court, infalsifiable, <b>scopé à la ZONE</b> (pas au shard) — c'est lui
/// qui unifie les déplacements intra et cross-zone sur l'unique chemin « ticket + reconnexion + ré-enregistrement »
/// (corrige la divergence V2.2). Remplace le « trust loopback + <c>atoi(&amp;tID[2])</c> » émergent du legacy par un
/// contrat explicite lié à <c>(compte, personnage, zone cible)</c>. Voir <c>Roadmap/FONDATIONS/06</c>.
/// </summary>
public readonly record struct HandoverTicket(
    Guid TicketId,
    int AccountId,
    int CharacterId,
    short TargetZone,
    long ExpiresAtUtcMs);
