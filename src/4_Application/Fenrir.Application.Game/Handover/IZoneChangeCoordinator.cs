using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Application.Game.Handover;

/// <summary>Motif de refus d'un changement de zone.</summary>
public enum ZoneChangeRejection : byte
{
    /// <summary>Aucun (accepté).</summary>
    None = 0,

    /// <summary>Zone cible = zone courante.</summary>
    SameZone = 1,

    /// <summary>Numéro de zone hors plage valide.</summary>
    OutOfRange = 2,

    /// <summary>Non autorisé (règle tribu / pierre-de-garde — <c>WrapCheck</c> legacy).</summary>
    NotAuthorized = 3,

    /// <summary>Zone cible hors ligne (port == 0 dans l'annuaire).</summary>
    TargetOffline = 4,
}

/// <summary>
/// Résultat d'un changement de zone. En cas d'acceptation, le client reçoit <see cref="Host"/>:<see cref="Port"/>
/// et un <see cref="TicketId"/> : il <b>ferme son socket et en ouvre un neuf</b> vers la zone cible.
/// </summary>
public readonly record struct ZoneChangeResult(
    bool Accepted,
    string? Host,
    int Port,
    Guid TicketId,
    ZoneChangeRejection Rejection);

/// <summary>
/// Coordinateur du changement de zone — <b>chemin UNIQUE</b> pour intra-zone ET cross-zone (corrige la divergence
/// V2.2 : le C# actuel ne reconnecte qu'en cross-shard). Invariant : à <b>chaque</b> changement de zone, on pousse
/// le snapshot au broker <b>avant</b> déconnexion, on mint un ticket scopé <b>zone</b>, on renvoie l'endpoint, et
/// on attend une <b>nouvelle connexion TCP</b>. Jamais de transfert interne sur le même socket.
/// Voir <c>Roadmap/FONDATIONS/04_ZoneServer.md</c> et <c>06_Session_et_Handover.md</c> (ADR-0004).
/// </summary>
/// <remarks>
/// TODO(F4) : seam <b>non encore implémenté</b> — aucune implémentation, injection ni résolution DI à ce jour.
/// Le flux cross-zone réel passe aujourd'hui par les paquets Login <c>ZoneTransferRequest/Response</c> ; ce
/// coordinateur unifiera intra + cross-zone au Lot F4 (avec <c>Fenrir.Cluster</c>). Ne pas supposer disponible.
/// </remarks>
public interface IZoneChangeCoordinator
{
    /// <summary>Traite une demande de changement de zone (op 20) selon le chemin unique « ticket + reconnexion ».</summary>
    ValueTask<ZoneChangeResult> RequestZoneChangeAsync(
        int accountId,
        int characterId,
        short currentZone,
        short targetZone,
        CancellationToken cancellationToken);
}
