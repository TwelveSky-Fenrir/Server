using System.IO.Pipelines;
using System.Net;
using Fenrir.Cluster;
using Fenrir.Cluster.Wire;
using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer;

/// <summary>
/// Session <b>serveur-à-serveur</b> acceptée par le CenterServer (un lien entrant d'une Zone ou du LoginServer).
/// Réutilise l'appareil d'envoi de <see cref="ClientSession"/> (<c>Send</c>/<c>SendRaw</c> + backpressure) : le
/// sens sortant Center→pair est déjà un cadre à en-tête d'opcode 1 octet (<c>WireHeaderSizes.DefaultPacketSize</c>,
/// ex-<c>SV_DEFAULT_PACKET</c>), exactement la convention S2S. Le lien n'applique <b>pas</b> le XOR client : la
/// clé <c>InboundStreamXorKey</c> reste à 0 (no-op) — le durcissement du lien passe par un handshake authentifié,
/// pas par le chiffrement de flux legacy.
/// </summary>
internal sealed class CenterLinkSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Center, remoteEndPoint, logger)
{
    /// <summary>État du lien : <c>Connected</c> jusqu'au handshake, puis <c>Authenticated</c>. Interdit qu'un pair
    /// non authentifié émette des events monde (le gate généré n'admet que le handshake avant transition).</summary>
    public CenterSessionState State { get; private set; } = CenterSessionState.Connected;

    /// <summary>Portillon d'opcode par état S2S, adossé au <c>CenterSessionStateGate</c> source-généré à partir des
    /// <c>AllowedStates</c> des paquets Center — appliqué avant le dispatch, élimine les paquets hors séquence.</summary>
    public override bool IsOpcodeAllowed(byte opcode)
    {
        return CenterSessionStateGate.Allows(State, opcode);
    }

    /// <summary>Bascule le lien en <c>Authenticated</c> après un handshake HMAC réussi (transition unique).</summary>
    public void MarkAuthenticated()
    {
        State = CenterSessionState.Authenticated;
    }
}
