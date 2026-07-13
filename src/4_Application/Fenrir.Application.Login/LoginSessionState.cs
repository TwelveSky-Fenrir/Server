namespace Fenrir.Application.Login;

/// <summary>
/// Machine à états d'une session cliente sur le LoginServer (appliquée <b>avant</b> le dispatch par la garde
/// d'état générée). Les ordinaux sont <b>load-bearing</b> : les paquets Login déclarent leurs
/// <c>AllowedStates</c> par valeur d'octet, et le <c>SessionStateGate</c> généré teste purement numériquement.
/// </summary>
public enum LoginSessionState : byte
{
    /// <summary>Socket accepté, avant négociation de version.</summary>
    Connected = 0,

    /// <summary>Version client validée.</summary>
    VersionOk = 1,

    /// <summary>Identifiants authentifiés.</summary>
    Authenticated = 2,

    /// <summary>Code PIN souris requis (création/vérification).</summary>
    PinRequired = 3,

    /// <summary>Sélection de personnage (roster chargé).</summary>
    CharSelect = 4,

    /// <summary>Ticket de handover émis : le client va se reconnecter à la zone cible.</summary>
    HandoverIssued = 5
}
