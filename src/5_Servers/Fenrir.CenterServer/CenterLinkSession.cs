using System.IO.Pipelines;
using System.Net;
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
    /// <summary>
    /// Portillon d'opcode par état de session S2S. <b>TODO(F4)</b> : à adosser à un <c>CenterSessionStateGate</c>
    /// source-généré (pré-handshake : seul l'op « hello » ; post-handshake : enregistrement/relais/op33/op57).
    /// Fail-closed en attendant : ce substrat ne fait pas encore tourner le <c>SessionLoop</c> (pas de codec S2S
    /// Center généré), donc cette surface n'est pas encore consultée — elle refuse tout par défaut.
    /// </summary>
    public override bool IsOpcodeAllowed(byte opcode)
    {
        return false;
    }
}
