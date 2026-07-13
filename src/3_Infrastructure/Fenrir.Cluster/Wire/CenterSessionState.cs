namespace Fenrir.Cluster.Wire;

/// <summary>
/// Machine à états d'un lien serveur-à-serveur entrant sur le CenterServer (appliquée <b>avant</b> le dispatch
/// par la garde d'état générée <c>CenterSessionStateGate</c>). Le lien est un pair du cluster (Zone/Login), pas un
/// client de jeu : la seule progression est l'authentification par secret partagé. Interdit qu'un pair non
/// authentifié émette des events monde/kick — durcit la faille legacy #8 (« seul un pair saurait » ≠ auth).
/// </summary>
public enum CenterSessionState : byte
{
    /// <summary>Lien TCP accepté, en attente du handshake d'authentification (HMAC sur défi). Seuls les opcodes
    /// de handshake sont admis dans cet état.</summary>
    Connected = 0,

    /// <summary>Handshake réussi : le pair est authentifié et peut s'enregistrer, émettre des events monde (op33)
    /// et recevoir le fan-out.</summary>
    Authenticated = 1,
}
