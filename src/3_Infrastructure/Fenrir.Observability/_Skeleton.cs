namespace Fenrir.Observability;

/// <summary>
/// Marqueur du squelette <c>Fenrir.Observability</c> (Lot F1 — Fondations). Home unique de
/// l'instrumentation native (correctif F7) : <c>Meter</c>/<c>ActivitySource</c>/<c>[LoggerMessage]</c>,
/// <c>game.EventLog</c> (équivalent natif de <c>ts25gamelog</c>) et health-checks TCP-probe.
/// Contenu réel migré/écrit au Lot F5. Voir <c>Roadmap/FONDATIONS/08_Observabilite_Native.md</c>.
/// </summary>
public static class AssemblyInfo
{
    /// <summary>Nom logique de l'assembly (repère de squelette).</summary>
    public const string Name = "Fenrir.Observability";
}
