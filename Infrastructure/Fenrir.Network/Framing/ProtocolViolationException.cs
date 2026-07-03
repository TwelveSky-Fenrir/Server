using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Framing;

/// <summary>
///     Raised by <see cref="FrameDecoder" /> for a frame the legacy client would never legitimately send
///     (unregistered opcode). Always ends the session — never a recoverable condition.
/// </summary>
public sealed class ProtocolViolationException(FenrirServer server, byte opcode)
    : Exception($"Unknown legacy opcode {opcode} for server {server}.")
{
    public FenrirServer Server { get; } = server;
    public byte Opcode { get; } = opcode;
}
