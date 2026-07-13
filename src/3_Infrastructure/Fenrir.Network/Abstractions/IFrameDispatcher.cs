using System.Buffers;
using Fenrir.Core.Abstractions;
using Fenrir.Core.Wire;

namespace Fenrir.Network.Abstractions;

/// <summary>
/// Contrat d'entrée du <c>SessionLoop</c> transport : route une trame (opcode + payload déchiffré) vers le
/// handler adéquat. Implémenté côté Application (<c>LoginFrameDispatcher</c>/<c>ZoneFrameDispatcher</c>), qui
/// voit ses paquets et handlers ; le transport reste serveur-agnostique.
/// </summary>
public interface IFrameDispatcher
{
    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session,
        CancellationToken cancellationToken);
}
