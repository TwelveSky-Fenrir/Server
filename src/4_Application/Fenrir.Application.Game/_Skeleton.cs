namespace Fenrir.Application.Game;

/// <summary>
/// Marqueur du squelette <c>Fenrir.Application.Game</c> (Lot F1 — Fondations). Slice serveur Game :
/// paquets Zone, <c>ZoneFrameDispatcher</c>, handlers, services et <b>acteur de zone</b> (tick + channel
/// mono-consommateur + AoI ; pièce maîtresse, doc 04). Absorbe <c>ts25extra</c>. Contenu migré/écrit aux
/// Lots F3 (ZoneServer) et F6. Voir <c>Roadmap/FONDATIONS/04_ZoneServer.md</c>.
/// </summary>
public static class AssemblyInfo
{
    /// <summary>Nom logique de l'assembly (repère de squelette).</summary>
    public const string Name = "Fenrir.Application.Game";
}
