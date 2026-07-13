namespace Fenrir.Domain.Game;

/// <summary>
/// Marqueur du squelette <c>Fenrir.Domain.Game</c> (Lot F1 — Fondations). Règles pures du domaine Game.
/// Le contenu réel (combat, stats, mouvement, progression, items, quêtes, social, world…) est migré au
/// Lot F6 depuis le god-project <c>Fenrir.Application.Game.Domain</c> (561 fichiers), redécoupé par
/// bounded-context (correctif F3). <c>ObfuscatedUidCodec</c> en sort vers <c>Fenrir.Core</c> (F11).
/// </summary>
public static class AssemblyInfo
{
    /// <summary>Nom logique de l'assembly (repère de squelette).</summary>
    public const string Name = "Fenrir.Domain.Game";
}
