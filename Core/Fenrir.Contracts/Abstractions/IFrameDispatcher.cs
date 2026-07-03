using System.Buffers;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Abstractions;

/// <summary>
///     Routes a decoded frame to whatever handles it. Defined in Core so Fenrir.Network (Infrastructure) never
///     depends on the Application-layer handler wiring for a specific executable — each exe's Program.cs supplies
///     its own concrete implementation (bridging to its generated MessageDispatcher) via DI.
/// </summary>
public interface IFrameDispatcher
{
    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session,
        CancellationToken cancellationToken);
}
