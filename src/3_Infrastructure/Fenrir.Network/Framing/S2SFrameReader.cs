using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Framing;

/// <summary>
/// Sibling de <see cref="FrameReader"/> pour le lien <b>serveur-à-serveur</b> (S2S) du CenterServer.
/// Contrat, signature et gestion de la fragmentation identiques à <see cref="FrameReader"/> — à une seule
/// différence près : l'en-tête S2S est un opcode sur <b>1 octet</b> (<c>WireHeaderSizes.DefaultPacketSize</c>,
/// ex-<c>SV_DEFAULT_PACKET</c>) lu à l'<b>offset 0</b>, là où le cadre CLIENT lit un en-tête de 9 octets avec
/// l'opcode à l'offset 8. Toujours <b>pas de length-prefix</b> : la taille de trame TOTALE (en-tête 1 o inclus,
/// ex. op33 → 135) vient de <see cref="IOpcodeFrameSizeProvider"/> (le <c>CenterOpcodeRegistry.Provider</c> généré),
/// exactement comme <see cref="FrameReader"/> consomme <c>LoginOpcodeRegistry</c>/<c>ZoneOpcodeRegistry</c>. Le lien
/// S2S est <b>en clair</b> : aucun XOR n'est appliqué ici (le <c>ReceiveLoopAsync</c> transport reste no-op tant que
/// <c>InboundStreamXorKey</c> vaut 0).
/// </summary>
public static class S2SFrameReader
{
    public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, IOpcodeFrameSizeProvider registry,
        FenrirServer server, out Frame frame)
    {
        frame = default;

        // Attendre au moins l'octet d'opcode (en-tête S2S = 1 o).
        if (buffer.Length < WireHeaderSizes.DefaultPacketSize)
            return false;

        Span<byte> header = stackalloc byte[WireHeaderSizes.DefaultPacketSize];
        buffer.Slice(0, WireHeaderSizes.DefaultPacketSize).CopyTo(header);
        var opcode = header[0];

        // Opcode inconnu du registre Center → violation de protocole, non récupérable : l'appelant
        // (la boucle de dispatch S2S) coupe le lien, même posture que FrameReader côté client.
        if (!registry.TryGetFrameSize(opcode, out var frameSize))
            throw new ProtocolViolationException(server, opcode);

        // Attendre la trame complète (taille totale, en-tête 1 o inclus) avant de la découper.
        if (buffer.Length < frameSize)
            return false;

        var payload = buffer.Slice(WireHeaderSizes.DefaultPacketSize, frameSize - WireHeaderSizes.DefaultPacketSize);
        frame = new Frame(server, opcode, payload);
        buffer = buffer.Slice(frameSize);
        return true;
    }
}
