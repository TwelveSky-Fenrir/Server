using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Cluster.Sessions;

/// <summary>
/// Émission et consommation atomique des tickets de handover (adossé à <c>runtime.SessionTickets</c>, In-Memory
/// OLTP, proc native <c>Consume</c> = lecture + suppression atomiques). La consommation unique rend le rejeu
/// impossible même sous course entre deux connexions (TTL court, ~15 s).
/// </summary>
public interface IHandoverTicketService
{
    /// <summary>Émet un ticket lié à <c>(compte, personnage, zone cible)</c> et retourne son identifiant.</summary>
    ValueTask<Guid> MintAsync(int accountId, int characterId, short targetZone, CancellationToken cancellationToken);

    /// <summary>Consomme un ticket <b>une seule fois</b> ; <c>null</c> si inconnu, expiré ou déjà consommé.</summary>
    ValueTask<HandoverTicket?> ConsumeAsync(Guid ticketId, CancellationToken cancellationToken);
}
