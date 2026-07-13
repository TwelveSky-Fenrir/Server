using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Security.Abstractions;

/// <summary>
/// Garde d'admission consultée <b>au socket accept</b> (correctif : aujourd'hui la blocklist n'est consultée
/// qu'à l'auth/entrée-monde, laissant une IP bloquée ouvrir des sockets — cf. <c>Roadmap/FONDATIONS/09</c>).
/// L'évaluation s'appuie sur un cache in-memory (anti-flood + blocklist unifiée) sans toucher SQL sur le hot path.
/// </summary>
public interface IConnectionGate
{
    /// <summary>Évalue une IP entrante ; <see cref="ConnectionVerdict.Block"/> ⇒ fermeture immédiate du socket.</summary>
    ValueTask<ConnectionVerdict> EvaluateAsync(IPAddress remoteAddress, CancellationToken cancellationToken);
}
