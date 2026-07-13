namespace Fenrir.Security.Abstractions;

/// <summary>
/// Authentificateur du lien serveur-à-serveur (CenterLink) : handshake défi-réponse HMAC-SHA256 basé sur un secret
/// partagé, pour que seuls les vrais pairs (LoginServer, GameServers) puissent établir un lien S2S avec le
/// CenterServer. Durcit la faille legacy #8 (kick <c>WM_COPYDATA</c> sans vérification d'origine : « seul un pair
/// saurait » n'est PAS de l'authentification — proximité/loopback ne remplacent jamais une preuve cryptographique).
/// </summary>
/// <remarks>
/// <para><b>Flux.</b> Le Center émet un nonce (<see cref="IssueChallenge"/>) → le client répond
/// <c>HMAC(secret, nonce ‖ context)</c> (<see cref="ComputeHelloMac"/>) → le Center recompute et compare en <b>temps
/// constant</b> (<see cref="VerifyHelloMac"/>). Le même secret partagé (env <c>Center__SharedSecret</c>) est câblé aux
/// deux extrémités par l'AppHost, d'où une API vivant en <c>Fenrir.Security</c> et consommée des deux côtés.</para>
/// <para><b>Fail-closed.</b> Secret vide/null ⇒ <see cref="IsEnabled"/> = <c>false</c> : <see cref="VerifyHelloMac"/>
/// renvoie toujours <c>false</c> (le Center refuse alors tout lien) tandis que <see cref="IssueChallenge"/> et
/// <see cref="ComputeHelloMac"/> lèvent. Aucune connexion ne peut être authentifiée en l'absence de secret.</para>
/// <para><b>Sans état / anti-rejeu.</b> L'authentificateur ne conserve aucun cache de nonce : un nonce aléatoire frais
/// par accept, gardé uniquement dans l'état de la connexion et vérifié une seule fois, suffit — une réponse capturée
/// est inutile contre le nonce d'une autre connexion. Le contexte optionnel lie le MAC à l'identité/rôle déclarés du
/// pair. Le secret partagé n'est jamais journalisé ni sérialisé.</para>
/// </remarks>
public interface ICenterLinkAuthenticator
{
    /// <summary>
    /// <c>true</c> si un secret partagé non vide est configuré. <c>false</c> ⇒ mode fail-closed (aucun lien
    /// authentifiable) : à consulter côté Center pour refuser d'emblée tout lien entrant plutôt que de tenter un
    /// handshake voué à échouer.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// [SERVEUR / Center] Produit un défi à usage unique : un nonce cryptographiquement aléatoire de
    /// <see cref="CenterLinkAuth.NonceSize"/> octets. À appeler une fois par lien accepté ; stocker le résultat dans
    /// l'état de CETTE connexion, en émettre <see cref="CenterLinkChallenge.Nonce"/> vers le client, puis le jeter
    /// après vérification. Lève <see cref="InvalidOperationException"/> si <see cref="IsEnabled"/> est <c>false</c>.
    /// </summary>
    CenterLinkChallenge IssueChallenge();

    /// <summary>
    /// [SERVEUR / Center] Recompute <c>HMAC(secret, challenge.Nonce ‖ context)</c> et le compare en <b>temps constant</b>
    /// (<c>CryptographicOperations.FixedTimeEquals</c>) à la réponse du client. Ne lève jamais : renvoie <c>false</c>
    /// (fail-closed) si le secret est absent, si <paramref name="clientMac"/> n'a pas exactement
    /// <see cref="CenterLinkAuth.MacSize"/> octets, ou si <paramref name="context"/> dépasse
    /// <see cref="CenterLinkAuth.MaxContextLength"/>.
    /// </summary>
    /// <param name="challenge">Le défi précédemment émis pour CETTE connexion (passé par <c>in</c>).</param>
    /// <param name="context">
    /// Contexte de liaison optionnel (p. ex. identité/rôle/shard déclarés du pair) — doit être identique à celui utilisé
    /// par le client ; vide si non utilisé.
    /// </param>
    /// <param name="clientMac">La réponse HMAC-SHA256 reçue du client.</param>
    /// <returns><c>true</c> si la réponse est authentique ; <c>false</c> sinon (fail-closed).</returns>
    bool VerifyHelloMac(in CenterLinkChallenge challenge, ReadOnlySpan<byte> context, ReadOnlySpan<byte> clientMac);

    /// <summary>
    /// [CLIENT / Login-Game] Calcule la réponse <c>HMAC(secret, nonce ‖ context)</c> au défi reçu et l'écrit dans
    /// <paramref name="destination"/> (≥ <see cref="CenterLinkAuth.MacSize"/> octets ; peut être une tranche du tampon
    /// d'émission, d'où zéro-alloc). Lève <see cref="InvalidOperationException"/> si <see cref="IsEnabled"/> est
    /// <c>false</c>, et <see cref="ArgumentException"/> si une taille est invalide.
    /// </summary>
    /// <param name="nonce">Le nonce reçu du Center — exactement <see cref="CenterLinkAuth.NonceSize"/> octets.</param>
    /// <param name="context">Contexte de liaison optionnel, identique à celui attendu par le Center.</param>
    /// <param name="destination">Tampon de sortie du MAC.</param>
    /// <returns>Le nombre d'octets écrits (<see cref="CenterLinkAuth.MacSize"/>).</returns>
    int ComputeHelloMac(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> context, Span<byte> destination);
}
