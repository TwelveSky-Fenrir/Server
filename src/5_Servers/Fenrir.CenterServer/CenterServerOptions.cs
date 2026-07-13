namespace Fenrir.CenterServer;

/// <summary>
/// Options du CenterServer, liées à la section de configuration <c>Center</c> (env <c>Center__Port</c> injectée
/// par l'AppHost). Endpoint <b>interne</b> (jamais client-facing), fidèle à <c>ts25center</c> (127.0.0.1:12003).
/// </summary>
public sealed class CenterServerOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Center";

    /// <summary>Port TCP interne d'écoute (fidélité legacy : 12003).</summary>
    public int Port { get; set; } = 12003;

    /// <summary>
    /// Secret partagé du lien S2S (env <c>Center__SharedSecret</c>, injecté par l'AppHost). Réserve la clé de
    /// configuration du futur handshake authentifié (durcissement de la faille legacy #8 : lien zone↔center en
    /// clair, confiance loopback). <b>TODO(F4)</b> : sa vérification n'est pas encore branchée — voir
    /// <c>CenterServerHost</c>. <c>null</c>/vide = mode confiance-loopback en attendant.
    /// </summary>
    public string? SharedSecret { get; set; }
}
