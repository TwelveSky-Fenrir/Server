namespace Fenrir.Application.Game.ZoneRuntime;

/// <summary>
/// Machine à états d'une session cliente sur une zone (appliquée <b>avant</b> le dispatch par la garde d'état
/// générée). Élimine une classe entière d'exploits (paquets hors séquence) sans un seul <c>if</c> écrit à la main.
/// </summary>
public enum ZoneSessionState : byte
{
    /// <summary>Socket accepté, avant présentation du ticket.</summary>
    Connected = 0,

    /// <summary>Ticket de handover consommé (identité établie côté serveur).</summary>
    TicketConsumed = 1,

    /// <summary>Ré-enregistrement en cours (hydratation de l'état joueur).</summary>
    Registering = 2,

    /// <summary>Entré en monde : reçoit les opcodes de jeu.</summary>
    InWorld = 3,

    /// <summary>Changement de zone en cours (« move in progress ») : anti-course teardown/claim.</summary>
    Leaving = 4,
}
