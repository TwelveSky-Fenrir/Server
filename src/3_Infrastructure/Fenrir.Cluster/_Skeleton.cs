namespace Fenrir.Cluster;

/// <summary>
/// Marqueur du squelette <c>Fenrir.Cluster</c> (Lot F1 — Fondations). Backbone de coordination cross-zone
/// (ex-<c>ts25center</c>, correctif F4) : annuaire de zones, relais d'events consolidés, ownership/hand-off
/// de session, coordination du world-state autoritaire (hero-rank/tribu/tour/alliances/party — bugs legacy
/// corrigés, pas répliqués). Rempli au Lot F4. Voir <c>Roadmap/FONDATIONS/05_CenterServer_Coordination.md</c>.
/// </summary>
public static class AssemblyInfo
{
    /// <summary>Nom logique de l'assembly (repère de squelette).</summary>
    public const string Name = "Fenrir.Cluster";
}
