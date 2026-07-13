namespace Fenrir.ServiceDefaults;

/// <summary>
/// Marqueur du squelette <c>Fenrir.ServiceDefaults</c> (Lot F1 — Fondations). Portera l'extension
/// <c>AddFenrirDefaults()</c> (export OTLP + service discovery), tirée uniquement par les exécutables.
/// Câblage réel au Lot F2. Distinct de <c>Fenrir.Observability</c> (qui porte les primitives
/// d'instrumentation). Voir <c>Roadmap/FONDATIONS/03_Topologie_TCP_et_Aspire.md</c>.
/// </summary>
public static class AssemblyInfo
{
    /// <summary>Nom logique de l'assembly (repère de squelette).</summary>
    public const string Name = "Fenrir.ServiceDefaults";
}
